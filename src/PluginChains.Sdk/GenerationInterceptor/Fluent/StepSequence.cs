namespace Ohirok.GenerationInterceptor.Fluent
{
    public sealed class StepSequence
    {
        private readonly List<StepItem> _steps;

        public StepItem First => _steps.Count > 0 ? _steps[0] : default;
        public StepItem Last => _steps.Count > 0 ? _steps[_steps.Count - 1] : default;
        public int Count => _steps.Count;
        public IReadOnlyList<StepItem> Items => _steps;

        internal StepSequence(List<StepItem> steps) => _steps = steps ?? new();

        public void WrapSequence(string before, string after, int order = 0)
        {
            if (_steps.Count == 0) return;
            if (!string.IsNullOrEmpty(before)) First.InjectBefore(before, order);
            if (!string.IsNullOrEmpty(after)) Last.InjectAfter(after, order);
        }

        public void WrapEach(string before, string after, int order = 0)
        {
            foreach (var step in _steps)
                step.Wrap(before, after, order);
        }
    }
}
