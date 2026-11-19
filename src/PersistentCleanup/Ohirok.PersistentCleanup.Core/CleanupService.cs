using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Ohirok.CustomBindings;
using Ohirok.PluginChains.Common;
using Reqnroll.BoDi;

namespace Ohirok.PersistentCleanup
{
    internal interface ICleanupStorageManager
    {
        void UseStorage(ICleanupStorage storage, StorageReplaceMode mode);
    }

    /// <summary>
    /// Orchestrates the registration and execution of persistent cleanup actions.
    /// </summary>
    public interface ICleanupService
    {
        /// <summary>
        /// Register a cleanup action for an instance method of <typeparamref name="T"/>
        /// (or a static method called inside the lambda). Returns the id of the persisted record.
        /// The arguments are evaluated and serialized to JSON right now.
        /// </summary>
        Task<string> AddAsync<T>(
            Expression<Action<T>> call,
            string description = null,
            int order = 10000,
            int? maxRetries = null,
            CancellationToken ct = default);

        /// <summary>
        /// Register a static cleanup action (also the way to use static classes). Returns the id of the persisted record.
        /// </summary>
        Task<string> AddAsync(
            Expression<Action> call,
            string description = null,
            int order = 10000,
            int? maxRetries = null,
            CancellationToken ct = default);

        /// <summary>
        /// Execute every leftover record of this service's scope (pending, abandoned, failed and orphan ones).
        /// </summary>
        Task<CleanupExecutionResult> PerformAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Execute one specific record by id, whatever its scope.
        /// </summary>
        Task<CleanupExecutionResult> PerformAsync(string id, CancellationToken ct = default);

        /// <summary>
        /// Delete a specific cleanup record without executing it.
        /// </summary>
        Task DeleteAsync(string id, CancellationToken ct = default);

        /// <summary>
        /// Get current statistics about the records of this service's scope.
        /// </summary>
        Task<CleanupStatistics> GetStatisticsAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Result of cleanup execution.
    /// </summary>
    public sealed class CleanupExecutionResult
    {
        /// <summary>Records this call tried to execute (excluding skipped ones).</summary>
        public int Total { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        /// <summary>Records whose cleanup method no longer exists in the code.</summary>
        public int Orphans { get; set; }
        /// <summary>Records left alone: already completed, owned by a live run, or claimed by another process first.</summary>
        public int Skipped { get; set; }
    }

    /// <summary>
    /// Statistics about cleanup records.
    /// </summary>
    public sealed class CleanupStatistics
    {
        public int Pending { get; set; }
        public int Processing { get; set; }
        public int Failed { get; set; }
        public int Orphans { get; set; }
    }

    public sealed class CleanupService(
        ICustomBindingProvider bindingProvider,
        IObjectContainer container,
        ICleanupStorage storage,
        CleanupConfigBase config) : ICleanupService, ICleanupStorageManager
    {
        private enum Outcome { Success, Failed, Orphan, Skipped }

        private static readonly CleanupStatus[] ExecutableStatuses =
        [
            CleanupStatus.Pending, CleanupStatus.Processing, CleanupStatus.Failed, CleanupStatus.Orphan
        ];

        readonly LateBoundStorageProxy<ICleanupStorage> _storageProxy = new(storage);
        readonly ConcurrentQueue<CleanupRecord> _buffer = new();
        readonly SemaphoreSlim _gate = new(1, 1);
        ICleanupStorage _storage = storage;
        ICleanupStorage _readyStorage;

        void ICleanupStorageManager.UseStorage(ICleanupStorage storage, StorageReplaceMode mode)
        {
            _storageProxy.UseStorage(storage, mode);
            _storage = _storageProxy.Current;
        }

        // ---------------------------------------------------------------- scope

        /// <summary>Scope written to new records.</summary>
        private string RecordScope =>
            string.IsNullOrWhiteSpace(config.Scope) || config.Scope.Trim() == CleanupConfigBase.AllScopes
                ? string.Empty
                : config.Scope.Trim();

        /// <summary>Scope used for queries; null means "every scope".</summary>
        private string QueryScope =>
            config.Scope != null && config.Scope.Trim() == CleanupConfigBase.AllScopes ? null : RecordScope;

        // ---------------------------------------------------------------- registration

        public Task<string> AddAsync<T>(
            Expression<Action<T>> call,
            string description = null,
            int order = 10000,
            int? maxRetries = null,
            CancellationToken ct = default)
        {
            if (call.Body is not MethodCallExpression methodCall)
                throw new ArgumentException("Expression must be a method call", nameof(call));

            return AddRecordAsync(methodCall, description, order, maxRetries, ct);
        }

        public Task<string> AddAsync(
            Expression<Action> call,
            string description = null,
            int order = 10000,
            int? maxRetries = null,
            CancellationToken ct = default)
        {
            if (call.Body is not MethodCallExpression methodCall)
                throw new ArgumentException("Expression must be a method call", nameof(call));

            if (!methodCall.Method.IsStatic)
                throw new ArgumentException("Method must be static. Use AddAsync<T>() for instance methods.", nameof(call));

            return AddRecordAsync(methodCall, description, order, maxRetries, ct);
        }

        private async Task<string> AddRecordAsync(
            MethodCallExpression methodCall, string description, int order, int? maxRetries, CancellationToken ct)
        {
            var record = BuildRecord(methodCall, description, order, maxRetries);

            var s = await EnsureReadyAsync(ct).ConfigureAwait(false);
            if (s == null)
            {
                // No storage yet (custom storage is configured later): keep the record in memory,
                // it is flushed as soon as a storage is set. The id is already valid.
                _buffer.Enqueue(record);
                return record.Id;
            }

            await s.SaveAsync(record, ct).ConfigureAwait(false);
            return record.Id;
        }

        private CleanupRecord BuildRecord(MethodCallExpression methodCall, string description, int order, int? maxRetries)
        {
            var method = methodCall.Method;

            if (method.GetCustomAttribute<CleanupAttribute>() == null)
                throw new ArgumentException(
                    $"Method '{method.DeclaringType?.Name}.{method.Name}' is not marked with [Cleanup]. " +
                    "Only [Cleanup] methods can be persisted and executed later.");

            var parameters = method.GetParameters();
            string argumentsJson;
            try
            {
                var arguments = methodCall.Arguments
                    .Select(a => Expression.Lambda(a).Compile().DynamicInvoke())
                    .ToArray();
                argumentsJson = JsonSerializer.Serialize(arguments);
                DeserializeArgs(argumentsJson, parameters); // fail now, not in the next run
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Arguments of cleanup method '{method.Name}' must not depend on the lambda parameter " +
                    $"and must survive a JSON round trip: {ex.Message}", ex);
            }

            return new CleanupRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Scope = RecordScope,
                TypeName = method.DeclaringType?.FullName,
                MethodKey = GetKey(method),
                Signature = GetSignature(method),
                ArgumentsJson = argumentsJson,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                Status = CleanupStatus.Pending,
                MaxRetries = Math.Max(1, maxRetries ?? config.DefaultMaxRetries),
                Order = order
            };
        }

        // ---------------------------------------------------------------- execution

        public async Task<CleanupExecutionResult> PerformAllAsync(CancellationToken ct = default)
        {
            var s = await RequireStorageAsync(ct).ConfigureAwait(false);
            var staleBefore = StaleBefore();

            var records = await s.GetByStatusesAsync(ExecutableStatuses, QueryScope, ct).ConfigureAwait(false);
            var candidates = records
                .Where(r => IsClaimable(r, staleBefore))
                .OrderBy(r => r.Order)
                .ThenBy(r => r.CreatedAt)
                .ToList();

            var result = new CleanupExecutionResult
            {
                Total = candidates.Count,
                Skipped = records.Count - candidates.Count
            };

            if (candidates.Count > 0)
            {
                Log(LogLevel.Brief, $"Performing {candidates.Count} leftover cleanup(s){ScopeSuffix()}.");

                var methods = LoadCleanupMethods();
                var semaphore = new SemaphoreSlim(Math.Max(1, config.MaxConcurrency));
                int successful = 0, failed = 0, orphans = 0, skipped = 0;

                // Lower Order values run first. Records sharing the same Order run in parallel (up to maxConcurrency).
                foreach (var group in candidates.GroupBy(r => r.Order).OrderBy(g => g.Key))
                {
                    await Task.WhenAll(group.Select(async record =>
                    {
                        await semaphore.WaitAsync(ct).ConfigureAwait(false);
                        try
                        {
                            switch (await ExecuteRecordAsync(s, record, methods, staleBefore, ct).ConfigureAwait(false))
                            {
                                case Outcome.Success: Interlocked.Increment(ref successful); break;
                                case Outcome.Failed: Interlocked.Increment(ref failed); break;
                                case Outcome.Orphan: Interlocked.Increment(ref orphans); break;
                                default: Interlocked.Increment(ref skipped); break;
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    })).ConfigureAwait(false);
                }

                result.Successful = successful;
                result.Failed = failed;
                result.Orphans = orphans;
                result.Skipped += skipped;
                result.Total -= skipped;

                Log(LogLevel.Brief,
                    $"Cleanup finished: {successful} done, {failed} failed, {orphans} orphan, {result.Skipped} skipped.");
            }

            await ApplyRetentionAsync(s, ct).ConfigureAwait(false);
            return result;
        }

        public async Task<CleanupExecutionResult> PerformAsync(string id, CancellationToken ct = default)
        {
            var s = await RequireStorageAsync(ct).ConfigureAwait(false);
            var record = await s.GetByIdAsync(id, ct).ConfigureAwait(false);
            if (record == null)
                return new CleanupExecutionResult();

            var staleBefore = StaleBefore();
            if (!IsClaimable(record, staleBefore))
                return new CleanupExecutionResult { Total = 0, Skipped = 1 };

            var outcome = await ExecuteRecordAsync(s, record, LoadCleanupMethods(), staleBefore, ct).ConfigureAwait(false);
            return new CleanupExecutionResult
            {
                Total = outcome == Outcome.Skipped ? 0 : 1,
                Successful = outcome == Outcome.Success ? 1 : 0,
                Failed = outcome == Outcome.Failed ? 1 : 0,
                Orphans = outcome == Outcome.Orphan ? 1 : 0,
                Skipped = outcome == Outcome.Skipped ? 1 : 0
            };
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            var s = await RequireStorageAsync(ct).ConfigureAwait(false);
            await s.DeleteAsync(id, ct).ConfigureAwait(false);
        }

        public async Task<CleanupStatistics> GetStatisticsAsync(CancellationToken ct = default)
        {
            var s = await RequireStorageAsync(ct).ConfigureAwait(false);
            var records = await s.GetByStatusesAsync(ExecutableStatuses, QueryScope, ct).ConfigureAwait(false);

            return new CleanupStatistics
            {
                Pending = records.Count(r => r.Status == CleanupStatus.Pending),
                Processing = records.Count(r => r.Status == CleanupStatus.Processing),
                Failed = records.Count(r => r.Status == CleanupStatus.Failed),
                Orphans = records.Count(r => r.Status == CleanupStatus.Orphan)
            };
        }

        private async Task<Outcome> ExecuteRecordAsync(
            ICleanupStorage s,
            CleanupRecord record,
            IReadOnlyList<MethodInfo> methods,
            DateTime staleBefore,
            CancellationToken ct)
        {
            var observed = record.Status;

            // Atomic: only one process wins. Everyone else skips the record.
            var claimed = await s.TryClaimAsync(
                record.Id, observed, observed == CleanupStatus.Processing ? staleBefore : (DateTime?)null, ct)
                .ConfigureAwait(false);
            if (!claimed)
                return Outcome.Skipped;

            var method = FindMethod(methods, record);
            if (method == null)
            {
                await s.UpdateStatusAsync(record.Id, CleanupStatus.Orphan,
                    $"Cleanup method '{record.TypeName}.{record.MethodKey}({record.Signature})' was not found in the loaded assemblies.",
                    null, ct).ConfigureAwait(false);
                Log(LogLevel.Brief, $"ORPHAN {Describe(record)}: cleanup method not found.");
                return Outcome.Orphan;
            }

            var maxAttempts = Math.Max(1, record.MaxRetries);
            string lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var instance = method.IsStatic ? null : container.Resolve(method.DeclaringType);
                    var args = DeserializeArgs(record.ArgumentsJson, method.GetParameters());
                    var invoked = method.Invoke(instance, args);

                    if (invoked is Task task)
                        await task.ConfigureAwait(false);

                    await s.UpdateStatusAsync(record.Id, CleanupStatus.Completed, null, attempt, ct).ConfigureAwait(false);

                    if (config.DeleteCompletedAfterDays <= 0)
                        await s.DeleteAsync(record.Id, ct).ConfigureAwait(false);

                    Log(LogLevel.Detailed, $"OK {Describe(record)}");
                    return Outcome.Success;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // the record stays Processing and is picked up again once it is stale
                }
                catch (Exception ex)
                {
                    var real = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                    lastError = $"{real.GetType().Name}: {real.Message}";

                    if (attempt < maxAttempts)
                        await s.UpdateStatusAsync(record.Id, CleanupStatus.Processing, lastError, attempt, ct)
                            .ConfigureAwait(false);
                }
            }

            await s.UpdateStatusAsync(record.Id, CleanupStatus.Failed, lastError, maxAttempts, ct).ConfigureAwait(false);
            Log(LogLevel.Brief, $"FAILED {Describe(record)} after {maxAttempts} attempt(s): {lastError}");
            return Outcome.Failed;
        }

        private async Task ApplyRetentionAsync(ICleanupStorage s, CancellationToken ct)
        {
            try
            {
                var now = DateTime.UtcNow;

                if (config.DeleteCompletedAfterDays > 0)
                    await s.DeleteWhereAsync(now.AddDays(-config.DeleteCompletedAfterDays), CleanupStatus.Completed, QueryScope, ct)
                        .ConfigureAwait(false);

                if (config.DeleteOrphansAfterDays > 0)
                    await s.DeleteWhereAsync(now.AddDays(-config.DeleteOrphansAfterDays), CleanupStatus.Orphan, QueryScope, ct)
                        .ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Log(LogLevel.Brief, $"Retention cleanup of old records failed: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------- storage plumbing

        /// <summary>
        /// Returns the storage, initialized exactly once and with buffered records flushed,
        /// or null when no storage is configured yet.
        /// </summary>
        private async Task<ICleanupStorage> EnsureReadyAsync(CancellationToken ct)
        {
            var s = _storage;
            if (s == null)
                return null;

            if (ReferenceEquals(_readyStorage, s) && _buffer.IsEmpty)
                return s;

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                s = _storage;
                if (s == null)
                    return null;

                if (!ReferenceEquals(_readyStorage, s))
                {
                    await s.InitializeAsync(ct).ConfigureAwait(false);
                    _readyStorage = s;
                }

                while (_buffer.TryDequeue(out var buffered))
                {
                    try
                    {
                        await s.SaveAsync(buffered, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        _buffer.Enqueue(buffered); // do not lose the record
                        throw;
                    }
                }

                return s;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<ICleanupStorage> RequireStorageAsync(CancellationToken ct)
        {
            var s = await EnsureReadyAsync(ct).ConfigureAwait(false);
            if (s == null)
                throw new InvalidOperationException(
                    "No cleanup storage is configured. Install a storage adapter package " +
                    "(for example Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin), or call UseStorage(...) " +
                    "from Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin.");
            return s;
        }

        // ---------------------------------------------------------------- helpers

        private DateTime StaleBefore() =>
            DateTime.UtcNow - TimeSpan.FromMinutes(Math.Max(1, config.ProcessingTimeoutMinutes));

        /// <summary>
        /// A record can be executed unless it is completed, or it is Processing and still "alive"
        /// (touched recently by a run that has not died).
        /// </summary>
        private static bool IsClaimable(CleanupRecord record, DateTime staleBefore)
        {
            switch (record.Status)
            {
                case CleanupStatus.Completed:
                    return false;
                case CleanupStatus.Processing:
                    return record.LastAttemptAt == null || record.LastAttemptAt.Value < staleBefore;
                default:
                    return true;
            }
        }

        private static string GetKey(MethodInfo method)
        {
            var key = method.GetCustomAttribute<CleanupAttribute>()?.Key;
            return string.IsNullOrEmpty(key) ? method.Name : key;
        }

        private static string GetSignature(MethodInfo method) =>
            string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName));

        private IReadOnlyList<MethodInfo> LoadCleanupMethods()
        {
            var methods = new List<MethodInfo>();
            foreach (var binding in bindingProvider.GetBindingsForAttribute<CleanupAttribute>())
            {
                if (binding.Method is Reqnroll.Bindings.Reflection.RuntimeBindingMethod rbm)
                    methods.Add(rbm.MethodInfo);
            }
            return methods;
        }

        private static MethodInfo FindMethod(IReadOnlyList<MethodInfo> methods, CleanupRecord record)
        {
            foreach (var method in methods)
            {
                if (method.DeclaringType?.FullName != record.TypeName) continue;
                if (GetKey(method) != record.MethodKey) continue;
                if (GetSignature(method) == record.Signature) return method;
            }
            return null;
        }

        private static object[] DeserializeArgs(string json, ParameterInfo[] parameters)
        {
            if (string.IsNullOrEmpty(json) || parameters.Length == 0)
                return [];

            var element = JsonSerializer.Deserialize<JsonElement>(json);
            return parameters
                .Select((p, i) => JsonSerializer.Deserialize(element[i].GetRawText(), p.ParameterType))
                .ToArray();
        }

        private string ScopeSuffix() =>
            QueryScope == null ? " (all scopes)" : QueryScope.Length == 0 ? string.Empty : $" (scope '{QueryScope}')";

        private static string Describe(CleanupRecord record) =>
            string.IsNullOrEmpty(record.Description)
                ? $"{record.MethodKey} [{record.Id}]"
                : $"{record.Description} [{record.Id}]";

        private void Log(LogLevel level, string message)
        {
            if (config.LogLevel >= level)
                Console.WriteLine("[Ohirok.PersistentCleanup] " + message);
        }
    }
}
