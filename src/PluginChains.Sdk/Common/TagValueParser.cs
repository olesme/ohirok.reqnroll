namespace Ohirok.PluginChains.Common
{
    /// <summary>
    /// Universal helper for parsing values from tags/hashtags.
    /// </summary>
    public static class TagValueParser
    {
        /// <summary>
        /// Extract values after keyword regardless of format:
        /// "xfail" -> []
        /// "xfail(MyException)" -> ["MyException"]
        /// "xfail[Ex1,Ex2]" -> ["Ex1", "Ex2"]
        /// "expected-fail:Ex1;Ex2;Ex3" -> ["Ex1", "Ex2", "Ex3"]
        /// "afail(Ex1|Ex2)" -> ["Ex1", "Ex2"]
        /// "afail-Exception" -> ["Exception"]
        /// </summary>
        public static bool TryExtractValues(string tag, string[] keywords, out string[] values, char[] separators = null)
        {
            separators ??= [',', ';', '|'];

            values = [];

            // Check if starts with any keyword
            string matched = null;
            foreach (var kw in keywords)
            {
                if (tag.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
                {
                    matched = kw;
                    break;
                }
            }

            if (matched == null) return false;

            // Get everything after keyword
            var afterKw = tag.Substring(matched.Length);
            if (string.IsNullOrEmpty(afterKw))
            {
                values = [];
                return true; // Valid, just no args
            }

            // Extract content from brackets/parentheses or directly
            var content = ExtractContent(afterKw);
            if (string.IsNullOrEmpty(content))
            {
                values = [];
                return true;
            }

            // Split by any separator
            var list = new List<string>();
            var parts = content.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (!string.IsNullOrEmpty(part))
                    list.Add(part);
            }

            values = [.. list];
            return true;
        }

        /// <summary>
        /// Extract content from: "(value)", "[value]", "{value}", ":value", "-value", "=value", "_value"
        /// </summary>
        private static string ExtractContent(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;

            // Remove leading delimiters
            int start = 0;
            while (start < s.Length && IsOpeningDelimiter(s[start])) start++;

            if (start >= s.Length) return null;

            // Find content end (closing bracket or end of string)
            int end = s.Length;

            // If we started with opening bracket, find matching closing
            if (start > 0)
            {
                char closingChar = GetClosingChar(s[start - 1]);
                int closeIdx = s.IndexOf(closingChar, start);
                if (closeIdx > start)
                    end = closeIdx;
            }

            return s.Substring(start, end - start).Trim();
        }

        private static bool IsOpeningDelimiter(char c) =>
            c == '(' || c == '[' || c == '{' || c == ':' || c == '=' || c == '-' || c == '_';

        private static char GetClosingChar(char opening) => opening switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            _ => '\0'
        };

        /// <summary>
        /// Try parse integer from tag with keywords
        /// "retry:3", "max(5)", "3attempts" -> 3
        /// </summary>
        public static bool TryParseInt(string tag, string[] keywords, out int value)
        {
            value = 0;

            // Check keyword first
            string matched = null;
            foreach (var kw in keywords)
            {
                if (tag.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
                {
                    matched = kw;
                    break;
                }
            }

            string numStr;
            if (matched != null)
            {
                // Extract after keyword
                var after = tag.Substring(matched.Length);
                numStr = ExtractContent(after);
            }
            else
            {
                // Try extract number from anywhere
                numStr = ExtractNumber(tag);
            }

            return !string.IsNullOrEmpty(numStr) && int.TryParse(numStr, out value);
        }

        private static string ExtractNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;

            // Try leading number
            int end = 0;
            while (end < s.Length && char.IsDigit(s[end])) end++;
            if (end > 0) return s.Substring(0, end);

            // Try trailing number
            int start = s.Length - 1;
            while (start >= 0 && char.IsDigit(s[start])) start--;
            if (start < s.Length - 1) return s.Substring(start + 1);

            // Try number anywhere after delimiter
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i]))
                {
                    int j = i;
                    while (j < s.Length && char.IsDigit(s[j])) j++;
                    return s.Substring(i, j - i);
                }
            }

            return null;
        }

        /// <summary>
        /// Parse TimeSpan from tag with keywords
        /// "timeout:30s", "delay(1m)", "500ms" -> TimeSpan
        /// </summary>
        public static bool TryParseTimeSpan(string tag, string[] keywords, out TimeSpan value)
        {
            value = TimeSpan.Zero;

            // Check keyword
            string matched = null;
            foreach (var kw in keywords)
            {
                if (tag.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
                {
                    matched = kw;
                    break;
                }
            }

            string timeStr;
            if (matched != null)
            {
                var after = tag.Substring(matched.Length);
                timeStr = ExtractContent(after);
            }
            else
            {
                timeStr = tag;
            }

            return TryParseTime(timeStr, out value);
        }

        /// <summary>
        /// Pattern matcher with intuitive syntax:
        /// - "Exception" -> exact match
        /// - "*Exception" -> ends with Exception (wildcard)
        /// - "Timeout*" -> starts with Timeout
        /// - "!Timeout*" -> NOT starts with Timeout (negation)
        /// - "regex:^(?!Timeout).*" -> full regex for complex cases
        /// - "/pattern/" -> regex shorthand
        /// </summary>
        public static bool MatchesPattern(string value, string pattern, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(value)) return false;

            // Handle negation prefix
            bool negate = pattern[0] == '!';
            if (negate) pattern = pattern.Substring(1).Trim();
            if (string.IsNullOrEmpty(pattern)) return !negate;

            bool matches;

            // Explicit regex mode
            if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                matches = MatchesRegex(value, pattern.Substring(6), comparison);
            }
            else if (pattern.StartsWith("/") && pattern.EndsWith("/") && pattern.Length > 2)
            {
                matches = MatchesRegex(value, pattern.Substring(1, pattern.Length - 2), comparison);
            }
            // Simple wildcards (* and ?) - convert to regex
            else if (pattern.IndexOfAny(['*', '?']) >= 0)
            {
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                matches = MatchesRegex(value, regexPattern, comparison);
            }
            // Exact match
            else
            {
                matches = value.Equals(pattern, comparison);
            }

            return negate ? !matches : matches;
        }

        /// <summary>
        /// Check if any value matches pattern rules with proper negative priority:
        /// - Negative patterns checked FIRST across ALL values (highest priority)
        /// - If ANY negative matches ANY value -> immediate false
        /// - Then check if ANY positive matches ANY value -> true
        /// - If only negatives and none matched -> true
        /// </summary>
        public static bool MatchesPatternRules(string[] values, string[] patterns, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (values == null || values.Length == 0) return false;
            if (patterns == null || patterns.Length == 0) return true;

            var validPatterns = patterns.Where(p => !string.IsNullOrWhiteSpace(p));
            if (!validPatterns.Any()) return true;

            var negatives = validPatterns.Where(p => p[0] == '!');
            var positives = validPatterns.Where(p => p[0] != '!');

            // STEP 1: If ANY negative matches ANY value -> fail
            if (negatives.Any(pattern => values.Any(value =>
                MatchesPattern(value, pattern.Substring(1), comparison))))
                return false;

            // STEP 2: If no positives, all negatives passed
            if (!positives.Any()) return true;

            // STEP 3: Check if ANY positive matches ANY value
            return positives.Any(pattern => values.Any(value => MatchesPattern(value, pattern, comparison)));
        }

        private static bool TryParseTime(string s, out TimeSpan value)
        {
            value = TimeSpan.Zero;
            if (string.IsNullOrEmpty(s)) return false;

            s = s.Trim();

            // Extract number
            int i = 0;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i == 0) return false;

            if (!double.TryParse(s.Substring(0, i), out double num)) return false;

            // Extract unit
            var unit = s.Substring(i).Trim().ToLowerInvariant();
            var multiplier = ParseTimeUnit(unit);

            if (multiplier < 0) return false;
            if (multiplier == 0) multiplier = 1000; // Default to seconds

            value = TimeSpan.FromMilliseconds(num * multiplier);
            return true;
        }

        private static double ParseTimeUnit(string unit)
        {
            if (string.IsNullOrEmpty(unit)) return 0;

            if (unit.StartsWith("ms") || unit.StartsWith("milli")) return 1;
            if (unit.StartsWith("s")) return 1000;
            if (unit.StartsWith("min") || (unit.Length == 1 && unit[0] == 'm')) return 60_000;
            if (unit.StartsWith("h")) return 3_600_000;

            return -1;
        }

        private static bool MatchesRegex(string value, string regexPattern, StringComparison comparison)
        {
            try
            {
                var options = (comparison == StringComparison.OrdinalIgnoreCase)
                    ? System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    : System.Text.RegularExpressions.RegexOptions.None;
                return System.Text.RegularExpressions.Regex.IsMatch(value, regexPattern, options);
            }
            catch
            {
                return false;
            }
        }
    }
}
