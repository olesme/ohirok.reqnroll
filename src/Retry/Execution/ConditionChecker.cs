namespace Ohirok.Retry.Execution
{
    internal static class ConditionChecker
    {
        public static bool ShouldRetry(string[] conditions, Exception ex)
        {
            if (conditions == null || conditions.Length == 0)
                return true;

            if (ex == null)
                return false;

            bool hasPositive = false;
            bool matchedPositive = false;

            for (int i = 0; i < conditions.Length; i++)
            {
                var cond = conditions[i];
                if (string.IsNullOrEmpty(cond)) continue;

                bool isNegative = cond[0] == '!';
                var pattern = isNegative ? cond.Substring(1) : cond;

                if (string.IsNullOrEmpty(pattern)) continue;

                if (isNegative)
                {
                    if (MatchesException(ex, pattern))
                        return false;
                }
                else
                {
                    hasPositive = true;
                    if (MatchesException(ex, pattern))
                        matchedPositive = true;
                }
            }

            return !hasPositive || matchedPositive;
        }

        private static bool MatchesException(Exception ex, string pattern)
        {
            var type = ex.GetType();
            while (type != null && type != typeof(object))
            {
                if (WildcardMatch(type.Name, pattern))
                    return true;

                if (pattern.Contains(".") && WildcardMatch(type.FullName, pattern))
                    return true;

                type = type.BaseType;
            }
            return false;
        }

        private static bool WildcardMatch(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            if (string.IsNullOrEmpty(text)) return pattern == "*";

            if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
                return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);

            if (pattern == "*") return true;

            if (pattern[0] == '*' && pattern.IndexOf('*', 1) < 0 && pattern.IndexOf('?') < 0)
            {
                var suffix = pattern.Substring(1);
                return text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern[pattern.Length - 1] == '*' && pattern.IndexOf('*') == pattern.Length - 1 && pattern.IndexOf('?') < 0)
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return WildcardMatchCore(text, pattern, 0, 0);
        }

        private static bool WildcardMatchCore(string text, string pattern, int ti, int pi)
        {
            while (pi < pattern.Length)
            {
                char pc = pattern[pi];

                if (pc == '*')
                {
                    while (pi < pattern.Length && pattern[pi] == '*') pi++;
                    if (pi >= pattern.Length) return true;

                    while (ti < text.Length)
                    {
                        if (WildcardMatchCore(text, pattern, ti, pi))
                            return true;
                        ti++;
                    }
                    return WildcardMatchCore(text, pattern, ti, pi);
                }

                if (ti >= text.Length) return false;

                if (pc == '?' || char.ToLowerInvariant(text[ti]) == char.ToLowerInvariant(pc))
                {
                    ti++;
                    pi++;
                }
                else
                {
                    return false;
                }
            }

            return ti >= text.Length;
        }
    }
}
