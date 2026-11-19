namespace Ohirok.PersistentCleanup
{
    /// <summary>
    /// Abstraction for persistent storage of cleanup records.
    /// Implementations must be thread-safe: records are saved and updated concurrently,
    /// and several test processes may use the same store at the same time.
    /// </summary>
    public interface ICleanupStorage
    {
        /// <summary>
        /// Initialize the storage (create tables, check connection, etc.). Called once before the first use.
        /// </summary>
        Task InitializeAsync(CancellationToken ct = default);

        /// <summary>
        /// Save a new record. <see cref="CleanupRecord.Id"/> is already assigned by the caller.
        /// </summary>
        Task SaveAsync(CleanupRecord record, CancellationToken ct = default);

        /// <summary>
        /// Get records having any of the given statuses, ordered by <see cref="CleanupRecord.Order"/>,
        /// then <see cref="CleanupRecord.CreatedAt"/>.
        /// </summary>
        /// <param name="statuses">Statuses to include.</param>
        /// <param name="scope">
        /// <c>null</c> = records of every scope; otherwise only records with exactly this scope
        /// (the empty string is the scope of records saved without a scope).
        /// </param>
        Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses,
            string scope = null,
            CancellationToken ct = default);

        /// <summary>
        /// Get a specific record by ID, or <c>null</c> if it does not exist.
        /// </summary>
        Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default);

        /// <summary>
        /// Atomically claim a record for execution: set its status to <see cref="CleanupStatus.Processing"/>
        /// and <see cref="CleanupRecord.LastAttemptAt"/> to now, but only if the record still has
        /// <paramref name="expectedStatus"/> and, when <paramref name="lastAttemptBefore"/> is given,
        /// its <c>LastAttemptAt</c> is empty or older than that moment.
        /// This is what guarantees that two processes never execute the same record.
        /// </summary>
        /// <returns><c>true</c> if this caller claimed the record; <c>false</c> if someone else changed it first.</returns>
        Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default);

        /// <summary>
        /// Update the status of a record. Also sets <c>LastAttemptAt</c> to now and <c>ErrorMessage</c> to the
        /// given value (<c>null</c> clears it). <c>AttemptCount</c> is changed only if a value is given.
        /// </summary>
        Task UpdateStatusAsync(
            string id,
            CleanupStatus status,
            string errorMessage = null,
            int? attemptCount = null,
            CancellationToken ct = default);

        /// <summary>
        /// Delete a specific record. Does nothing if it does not exist.
        /// </summary>
        Task DeleteAsync(string id, CancellationToken ct = default);

        /// <summary>
        /// Delete records matching all given criteria (a <c>null</c> criterion matches everything).
        /// </summary>
        /// <param name="olderThan">Delete only records created before this moment (UTC).</param>
        /// <param name="status">Delete only records with this status.</param>
        /// <param name="scope"><c>null</c> = every scope; otherwise only this exact scope.</param>
        /// <returns>Number of deleted records.</returns>
        Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default);
    }
}
