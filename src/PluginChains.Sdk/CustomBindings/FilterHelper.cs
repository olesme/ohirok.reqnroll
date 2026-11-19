namespace Ohirok.CustomBindings
{
    public static class FilterHelper
    {
        public static bool ShouldExecute(string[] filters, string[] states)
        {
            if (filters == null || filters.Length == 0)
                return true;

            if (states == null || states.Length == 0)
                return false;

            bool hasRequired = false;
            bool matchedRequired = false;

            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (string.IsNullOrEmpty(filter)) continue;

                bool isExclude = filter[0] == '!';
                var pattern = isExclude ? filter.Substring(1) : filter;

                if (isExclude)
                {
                    // If excluded pattern matches any state - don't execute
                    if (MatchesAny(states, pattern))
                        return false;
                }
                else
                {
                    hasRequired = true;
                    if (MatchesAny(states, pattern))
                        matchedRequired = true;
                }
            }

            return !hasRequired || matchedRequired;
        }

        private static bool MatchesAny(string[] states, string pattern)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (Matches(states[i], pattern))
                    return true;
            }
            return false;
        }

        public static bool Matches(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            if (string.IsNullOrEmpty(text)) return pattern == "*";

            // No wildcards - exact match
            if (!ContainsWildcard(pattern))
                return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);

            return WildcardMatch(text, pattern);
        }

        private static bool ContainsWildcard(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '*' || s[i] == '?')
                    return true;
            return false;
        }

        private static bool WildcardMatch(string text, string pattern)
        {
            int ti = 0, pi = 0;
            int starIdx = -1, matchIdx = 0;

            while (ti < text.Length)
            {
                if (pi < pattern.Length && (pattern[pi] == '?' || CharEqualIgnoreCase(text[ti], pattern[pi])))
                {
                    ti++;
                    pi++;
                }
                else if (pi < pattern.Length && pattern[pi] == '*')
                {
                    starIdx = pi++;
                    matchIdx = ti;
                }
                else if (starIdx >= 0)
                {
                    pi = starIdx + 1;
                    ti = ++matchIdx;
                }
                else
                {
                    return false;
                }
            }

            while (pi < pattern.Length && pattern[pi] == '*')
                pi++;

            return pi == pattern.Length;
        }

        private static bool CharEqualIgnoreCase(char a, char b)
        {
            if (a == b) return true;
            return char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
        }

        public static string[] GenerateStates(string[] prefixes, string[] values)
        {
            if (values == null || values.Length == 0)
                return [];

            if (prefixes == null || prefixes.Length == 0)
                return values;

            var result = new List<string>(prefixes.Length * values.Length);
            foreach (var prefix in prefixes)
            {
                foreach (var value in values)
                {
                    result.Add(prefix + value);
                }
            }
            return [.. result];
        }
    }
}
