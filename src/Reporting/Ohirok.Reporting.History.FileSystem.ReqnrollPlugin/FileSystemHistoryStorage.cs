using System.Text;
using System.Text.Json;

namespace Ohirok.Reporting.History.FileSystem.ReqnrollPlugin
{
    public sealed class FileSystemHistoryStorage : IReportHistoryStorage
    {
        private readonly string _filePath;
        private readonly bool _autoCreate;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public FileSystemHistoryStorage(string filePath, bool autoCreate = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            _filePath = Path.GetFullPath(filePath);
            _autoCreate = autoCreate;
        }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_autoCreate) return Task.CompletedTask;

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(_filePath))
                File.Create(_filePath).Dispose();

            return Task.CompletedTask;
        }

        /// <summary>Upserts by RunId (Option A): when <paramref name="snapshot"/>.RunId is
        /// non-null and an existing line in the file has the same RunId, the two are merged (see
        /// <see cref="ReportHistorySnapshotMerger"/>) and the WHOLE file is rewritten atomically - a plain
        /// append can no longer be used once a prior line might need to change. The rewrite itself is
        /// crash-safe: the merged content is written to a temp file first, then swapped in with
        /// File.Replace (atomic on the same volume, and available on netstandard2.0 unlike File.Move's
        /// overwrite overload), so a crash mid-write never leaves the real file truncated/partial.</summary>
        public async Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                var snapshots = ReadAllSnapshots();

                var matchIndex = snapshot.RunId == null
                    ? -1
                    : snapshots.FindIndex(s => s.RunId == snapshot.RunId);

                if (matchIndex >= 0)
                    snapshots[matchIndex] = ReportHistorySnapshotMerger.Merge(snapshots[matchIndex], snapshot);
                else
                    snapshots.Add(snapshot);

                var builder = new StringBuilder();
                foreach (var s in snapshots)
                    builder.Append(JsonSerializer.Serialize(s)).Append('\n');

                var tempPath = _filePath + "." + Guid.NewGuid().ToString("n") + ".tmp";
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());

                using (var tempStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    await tempStream.WriteAsync(bytes, 0, bytes.Length, ct);
                }

                if (File.Exists(_filePath))
                    File.Replace(tempPath, _filePath, null);
                else
                    File.Move(tempPath, _filePath);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        {
            var snapshots = ReadAllSnapshots()
                .OrderByDescending(snapshot => snapshot.RunAt)
                .Take(count)
                .ToList();

            return Task.FromResult<IReadOnlyList<ReportHistoryRunSnapshot>>(snapshots);
        }

        private List<ReportHistoryRunSnapshot> ReadAllSnapshots()
        {
            if (!File.Exists(_filePath))
                return new List<ReportHistoryRunSnapshot>();

            return File.ReadLines(_filePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<ReportHistoryRunSnapshot>(line))
                .Where(snapshot => snapshot != null)
                .Select(snapshot => snapshot!)
                .ToList();
        }
    }
}
