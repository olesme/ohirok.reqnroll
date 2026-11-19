namespace Ohirok.GenerationInterceptor.Fluent
{
    internal sealed class InjectionScheduler
    {
        private readonly Dictionary<int, List<(string Code, int Order)>> _before = [];
        private readonly Dictionary<int, List<(string Code, int Order)>> _after = [];

        public void BeforeStep(int index, string code, int order = 0)
        {
            if (string.IsNullOrEmpty(code) || index < 0) return;
            if (!_before.TryGetValue(index, out var list))
                _before[index] = list = [];
            list.Add((code, order));
        }

        public void AfterStep(int index, string code, int order = 0)
        {
            if (string.IsNullOrEmpty(code) || index < 0) return;
            if (!_after.TryGetValue(index, out var list))
                _after[index] = list = [];
            list.Add((code, order));
        }

        internal IEnumerable<string> GetBefore(int index)
        {
            if (!_before.TryGetValue(index, out var list)) yield break;
            foreach (var item in list.OrderBy(x => x.Order))
                yield return item.Code;
        }

        internal IEnumerable<string> GetAfter(int index)
        {
            if (!_after.TryGetValue(index, out var list)) yield break;
            foreach (var (Code, _) in list.OrderByDescending(x => x.Order))
                yield return Code;
        }
    }
}
