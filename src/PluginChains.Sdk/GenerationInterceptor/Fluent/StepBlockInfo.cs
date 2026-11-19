using Gherkin.Ast;

namespace Ohirok.GenerationInterceptor.Fluent
{
    public sealed class StepBlockInfo
    {
        private readonly List<StepItem> _items;

        internal StepBlockInfo(IReadOnlyList<Step> steps, InjectionScheduler scheduler, Func<int, string[]> getHashtags)
        {
            _items = [.. steps.Select((step, i) =>
                new StepItem(step, i, steps.Count, scheduler, getHashtags))];
        }

        public int Count => _items.Count;
        public StepItem this[int index] => index >= 0 && index < _items.Count ? _items[index] : default;
        public StepItem First => Count > 0 ? _items[0] : default;
        public StepItem Last => Count > 0 ? _items[Count - 1] : default;

        public IEnumerable<StepItem> Where(Func<StepItem, bool> predicate) => _items.Where(predicate);

        public StepItem? Find(Func<StepItem, bool> predicate)
        {
            var item = _items.FirstOrDefault(predicate);
            return item.Step != null ? item : (StepItem?)null;
        }

        public int FindIndex(Func<StepItem, bool> predicate, int startFrom = 0)
        {
            for (int i = startFrom; i < _items.Count; i++)
                if (predicate(_items[i])) return i;
            return -1;
        }

        public StepSequence FindSequence(
            Func<StepItem, bool> from,
            Func<StepItem, bool> to = null,
            Func<StepItem, bool> until = null)
        {
            if (to != null && until != null)
                throw new ArgumentException("Use either 'to' (inclusive) or 'until' (exclusive), not both");

            var terminator = to ?? until;
            bool exclusive = until != null;

            int startIdx = FindIndex(from);
            if (startIdx < 0) return null;

            int endIdx = _items.Count - 1;
            if (terminator != null)
            {
                int found = FindIndex(terminator, startIdx + 1);
                if (found >= 0)
                    endIdx = exclusive ? found - 1 : found;
            }
            if (endIdx < startIdx) return null;

            return new StepSequence(_items.GetRange(startIdx, endIdx - startIdx + 1));
        }

        public IEnumerable<StepSequence> FindSequences(
            Func<StepItem, bool> from,
            Func<StepItem, bool> to = null,
            Func<StepItem, bool> until = null)
        {
            if (to != null && until != null)
                throw new ArgumentException("Use either 'to' (inclusive) or 'until' (exclusive), not both");

            var terminator = to ?? until;
            bool exclusive = until != null;

            int searchFrom = 0;
            while (searchFrom < _items.Count)
            {
                int startIdx = FindIndex(from, searchFrom);
                if (startIdx < 0) yield break;

                int endIdx = _items.Count - 1;
                if (terminator != null)
                {
                    int found = FindIndex(terminator, startIdx + 1);
                    if (found >= 0)
                        endIdx = exclusive ? found - 1 : found;
                }

                if (endIdx >= startIdx)
                    yield return new StepSequence(_items.GetRange(startIdx, endIdx - startIdx + 1));

                searchFrom = endIdx + 1;
            }
        }

        public void WrapAll(string before, string after, int order = 0)
        {
            if (Count == 0) return;
            First.InjectBefore(before, order);
            Last.InjectAfter(after, order);
        }
    }
}
