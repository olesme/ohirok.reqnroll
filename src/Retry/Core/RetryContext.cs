using Reqnroll;

namespace Ohirok.Retry.Core
{
    public sealed class RetryContext : ReqnrollContext
    {
        public RetryType Type { get; internal set; }
        /// <summary>Total runs allowed, the first one included. <c>@retry:2</c> and <c>@attempts:3</c> both give 3.</summary>
        public int MaxAttempts { get; set; }

        /// <summary>Retries allowed after the first run: <see cref="MaxAttempts"/> - 1, the same value seen the other way.</summary>
        public int MaxRetries
        {
            get => Math.Max(0, MaxAttempts - 1);
            set => MaxAttempts = Math.Max(0, value) + 1;
        }
        public string[] Conditions { get; set; }
        public TimeSpan Timeout { get; set; }
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// Set by an <see cref="Attributes.OnRetryDecisionAttribute"/> hook (via <see cref="ForceRetry"/>/<see cref="ForceStop"/>)
        /// to override the built-in retry-or-not verdict for the attempt that just failed. Reset to <c>null</c> at the start
        /// of every attempt. If several matching hooks run (ordered via <c>Order</c>), the last one to call ForceRetry/ForceStop wins.
        /// </summary>
        public bool? DecisionOverride { get; internal set; }

        public string[] FeatureTags { get; internal set; }
        public string[] RuleTags { get; internal set; }
        public string[] ScenarioTags { get; internal set; }
        public string[] Hashtags { get; internal set; }
        public string[] AllTags { get; internal set; }

        public int Attempt { get; internal set; }
        public RetryStatus Status { get; internal set; }
        public Exception LastException { get; internal set; }
        public Exception FirstException { get; internal set; }
        public DateTime LoopStartTime { get; internal set; }
        public DateTime AttemptStartTime { get; internal set; }

        public int Repeat => Attempt > 0 ? Attempt - 1 : 0;
        public bool IsSuccess => Status == RetryStatus.Success;
        public bool IsFailed => Status == RetryStatus.Failed || Status == RetryStatus.TimedOut;
        public TimeSpan Elapsed => LoopStartTime != default ? DateTime.UtcNow - LoopStartTime : TimeSpan.Zero;
        public TimeSpan AttemptElapsed => AttemptStartTime != default ? DateTime.UtcNow - AttemptStartTime : TimeSpan.Zero;

        internal ScenarioContext CurrentScenarioContext { get; set; }
        internal Dictionary<string, object> ScenarioBackup { get; set; }
        internal bool HasFailedInCurrentAttempt { get; set; }
        internal bool ShouldContinueLoop { get; set; }

        private readonly List<string> _hookFailures = [];

        /// <summary>
        /// <c>AfterScenario</c> hook failures of the attempts that were retried, shown as warnings on the final
        /// attempt. A hook failure on the final attempt fails the test as usual and is not listed here.
        /// </summary>
        public IReadOnlyList<string> HookFailures => _hookFailures;

        internal void AddHookFailure(string failure) => _hookFailures.Add(failure);

        /// <summary>True while Retry ends an attempt it is about to retry (that attempt's AfterScenario hooks run).</summary>
        internal bool EndingRetriedAttempt { get; set; }

        public RetryContext()
        {
            MaxAttempts = Config.RetryConfig.MaxAttempts;
            Conditions = Config.RetryConfig.Conditions;
            Delay = Config.RetryConfig.Delay;
            Timeout = TimeSpan.Zero;
            FeatureTags = [];
            RuleTags = [];
            ScenarioTags = [];
            Hashtags = [];
            AllTags = [];
            Status = RetryStatus.None;
        }

        internal void InitFromScenarioContext(ScenarioContext sc, string[] hashtags = null)
        {
            CurrentScenarioContext = sc;
            Hashtags = hashtags ?? [];

            var fc = sc.ScenarioContainer.Resolve<FeatureContext>();
            FeatureTags = fc?.FeatureInfo?.Tags ?? [];
            RuleTags = sc.RuleInfo?.Tags ?? [];
            ScenarioTags = sc.ScenarioInfo?.Tags ?? [];

            RebuildAllTags();
        }

        internal void RebuildAllTags()
        {
            int total = FeatureTags.Length + RuleTags.Length + ScenarioTags.Length + Hashtags.Length;
            if (total == 0) { AllTags = Array.Empty<string>(); return; }

            var all = new string[total];
            int idx = 0;
            FeatureTags.CopyTo(all, idx); idx += FeatureTags.Length;
            RuleTags.CopyTo(all, idx); idx += RuleTags.Length;
            ScenarioTags.CopyTo(all, idx); idx += ScenarioTags.Length;
            Hashtags.CopyTo(all, idx);
            AllTags = all;
        }

        internal void StartLoop()
        {
            LoopStartTime = DateTime.UtcNow;
            Status = RetryStatus.Running;
        }

        internal void StartAttempt()
        {
            Attempt++;
            AttemptStartTime = DateTime.UtcNow;
            LastException = null;
            HasFailedInCurrentAttempt = false;
            DecisionOverride = null;
        }

        /// <summary>The most recent failure of any attempt - unlike <see cref="LastException"/> it is not cleared
        /// when a new attempt starts, so a timeout raised before the current attempt failed can still report it.</summary>
        internal Exception? LastFailure { get; private set; }

        internal void RecordError(Exception ex)
        {
            LastFailure = ex;
            LastException = ex;
            FirstException ??= ex;
            HasFailedInCurrentAttempt = true;
        }

        internal void Complete(RetryStatus status) => Status = status;

        /// <summary>Called from an <see cref="Attributes.OnRetryDecisionAttribute"/> hook to force a retry despite conditions/status not matching. Still subject to MaxAttempts/Timeout.</summary>
        public void ForceRetry() => DecisionOverride = true;

        /// <summary>Called from an <see cref="Attributes.OnRetryDecisionAttribute"/> hook to force the loop to stop despite conditions/status matching.</summary>
        public void ForceStop() => DecisionOverride = false;

        internal bool CheckTimeout() => Timeout > TimeSpan.Zero && Elapsed > Timeout;

        public void Dispose()
        {
            CurrentScenarioContext = null;
            ScenarioBackup = null;
            LastException = null;
            FirstException = null;
            base.Dispose();
        }
    }
}
