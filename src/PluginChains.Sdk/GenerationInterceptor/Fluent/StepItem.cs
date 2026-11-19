using Gherkin.Ast;

namespace Ohirok.GenerationInterceptor.Fluent
{
    public readonly struct StepItem
    {
        private readonly InjectionScheduler _scheduler;
        private readonly Func<int, string[]> _getHashtags;

        public int Index { get; }
        public Step Step { get; }
        public string Keyword => Step?.Keyword?.Trim() ?? "";
        public string Text => Step?.Text ?? "";
        public int Line => Step?.Location.Line ?? 0;
        public bool IsFirst { get; }
        public bool IsLast { get; }

        internal StepItem(Step step, int index, int total, InjectionScheduler scheduler, Func<int, string[]> getHashtags)
        {
            Step = step;
            Index = index;
            IsFirst = index == 0;
            IsLast = index == total - 1;
            _scheduler = scheduler;
            _getHashtags = getHashtags;
        }

        public string[] Hashtags => _getHashtags?.Invoke(Index) ?? Array.Empty<string>();

        public bool HasHashtag(string tag) =>
            Hashtags.Any(h => h.Equals(tag, StringComparison.OrdinalIgnoreCase));

        public bool HasHashtagStartingWith(string prefix) =>
            Hashtags.Any(h => h.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        public string GetHashtagValue(string prefix) =>
            Hashtags.FirstOrDefault(h => h.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                ?.Substring(prefix.Length);

        public void InjectBefore(string code, int order = 0) => _scheduler?.BeforeStep(Index, code, order);
        public void InjectAfter(string code, int order = 0) => _scheduler?.AfterStep(Index, code, order);

        public void Wrap(string before, string after, int order = 0)
        {
            InjectBefore(before, order);
            InjectAfter(after, order);
        }
    }
}
