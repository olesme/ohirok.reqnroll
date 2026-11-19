using Io.Cucumber.Messages.Types;

namespace Ohirok.Reporting.Parsing
{
    /// <summary>
    /// Groups every <see cref="TestCaseStarted"/>/<see cref="TestCaseFinished"/> pair sharing the same
    /// <c>TestCaseId</c> into one ordered attempt list per scenario (on retry,
    /// Reqnroll publishes a full extra TestCaseStarted/TestCaseFinished pair sharing the same TestCaseId, with
    /// <c>TestCaseFinished.WillBeRetried == true</c> on every attempt but the last).
    /// </summary>
    public static class RetryAttemptGrouper
    {
        public sealed class AttemptPair
        {
            public TestCaseStarted Started { get; set; } = null!;
            public TestCaseFinished? Finished { get; set; }
        }

        /// <summary>Ordered oldest (Attempt 0) to newest, keyed by the shared TestCaseId.</summary>
        public static Dictionary<string, List<AttemptPair>> GroupByTestCaseId(
            IReadOnlyDictionary<string, TestCaseStarted> testCaseStartedById,
            IReadOnlyDictionary<string, TestCaseFinished> testCaseFinishedByStartedId)
        {
            return testCaseStartedById.Values
                .GroupBy(s => s.TestCaseId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(s => s.Attempt)
                        .Select(s => new AttemptPair
                        {
                            Started = s,
                            Finished = testCaseFinishedByStartedId.TryGetValue(s.Id, out var f) ? f : null
                        })
                        .ToList());
        }

        /// <summary>The attempt that ultimately determines the scenario's ongoing outcome - the first one not
        /// flagged for retry, or defensively the last attempt if every one of them (unexpectedly) says otherwise.</summary>
        public static AttemptPair FinalAttempt(List<AttemptPair> attempts)
            => attempts.FirstOrDefault(p => p.Finished?.WillBeRetried != true) ?? attempts[attempts.Count - 1];
    }
}
