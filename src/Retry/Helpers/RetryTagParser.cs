using Ohirok.Retry.Core;

namespace Ohirok.Retry.Helpers
{
    internal static class RetryTagParser
    {
        /// <summary>
        /// True for a tag/hashtag this parser gives meaning to (retry activation, attempts, timeout, delay, conditions).
        /// The generator passes only these to the runtime, so other plugins' hashtags (#xfail, #ignore, ...) never
        /// travel with Retry's own parameters - general step metadata stays the StepHashtags plugin's job.
        /// </summary>
        public static bool IsRetrySetting(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            if (tag[0] == '@' || tag[0] == '#') tag = tag.Substring(1);
            if (tag.Length == 0) return false;
            return tag.StartsWith(RetryConstants.RetryTag, StringComparison.OrdinalIgnoreCase)
                || TryParseAttempts(tag, out _)
                || TryParseTimeout(tag, out _)
                || TryParseDelay(tag, out _)
                || TryParseConditions(tag, new List<string>());
        }

        public static void ParseTags(RetryContext ctx)
        {
            var tags = ctx.AllTags;
            if (tags == null || tags.Length == 0) return;

            var conditions = new List<string>();

            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                if (string.IsNullOrEmpty(tag)) continue;

                if (tag[0] == '@' || tag[0] == '#')
                    tag = tag.Substring(1);

                if (tag.Length == 0) continue;

                if (TryParseAttempts(tag, out int attempts))
                {
                    ctx.MaxAttempts = attempts;
                }
                else if (TryParseTimeout(tag, out var timeout))
                {
                    ctx.Timeout = timeout;
                }
                else if (TryParseDelay(tag, out var delay))
                {
                    ctx.Delay = delay;
                }
                else if (TryParseConditions(tag, conditions))
                {
                    // just go ahead
                }
            }

            if (conditions.Count > 0)
                ctx.Conditions = [.. conditions];
        }

        #region Attempts Parsing

        // "retryblock" must come before "retry": otherwise "retryblock:2" matches "retry", leaves "block:2", and the
        // block silently falls back to the default attempt count.
        private static readonly string[] AttemptsKeywords = { "retryblock", "retry", "retries", "attempts", "times", "max", "tries" };

        private static bool TryParseAttempts(string tag, out int value)
        {
            value = 0;

            if (char.IsDigit(tag[0]))
            {
                int numEnd = 0;
                while (numEnd < tag.Length && char.IsDigit(tag[numEnd])) numEnd++;

                if (numEnd > 0 && int.TryParse(tag.Substring(0, numEnd), out value))
                {
                    var suffix = tag.Substring(numEnd).TrimStart('_', '-');
                    if (suffix.Length > 0 && MatchesAnyPrefix(suffix, AttemptsKeywords))   // a bare number says nothing: "7" is not attempts, "7times" is
                        return value > 0;
                }
                value = 0;
                return false;
            }

            foreach (var kw in AttemptsKeywords)
            {
                if (StartsWithIgnoreCase(tag, kw))
                {
                    var afterKw = tag.Substring(kw.Length);
                    if (TryExtractNumber(afterKw, out value))   // keyword + delimiter + number
                        return value > 0;
                }
            }

            return false;
        }

        #endregion

        #region Time Parsing

        private static readonly string[] TimeoutKeywords = { "timeout", "limit", "maxtime" };
        private static readonly string[] DelayKeywords = { "delay", "wait", "pause", "interval", "sleep" };

        private static bool TryParseTimeout(string tag, out TimeSpan value)
        {
            value = TimeSpan.Zero;
            return TryParseTimeWithKeywords(tag, TimeoutKeywords, out value);
        }

        private static bool TryParseDelay(string tag, out TimeSpan value)
        {
            return TryParseTimeWithKeywords(tag, DelayKeywords, out value);
        }

        private static bool TryParseTimeWithKeywords(string tag, string[] keywords, out TimeSpan value)
        {
            value = TimeSpan.Zero;

            foreach (var kw in keywords)
            {
                if (StartsWithIgnoreCase(tag, kw))
                {
                    var afterKw = tag.Substring(kw.Length);
                    return TryParseTimeValue(afterKw, requireDelimiter: true, requireUnit: false, out value);
                }
            }

            if (char.IsDigit(tag[0]))
            {
                return TryParseTimeValue(tag, requireDelimiter: false, requireUnit: true, out value);
            }

            return false;
        }

        private static bool TryParseTimeValue(string s, bool requireDelimiter, bool requireUnit, out TimeSpan value)
        {
            value = TimeSpan.Zero;
            if (string.IsNullOrEmpty(s)) return false;

            int start = 0;
            while (start < s.Length && IsDelimiter(s[start])) start++;
            if (requireDelimiter && start == 0) return false;   // "timeout:30s" yes, "timeoutfoo" / "timeout30s" no
            if (start >= s.Length) return false;

            int numStart = start;
            while (start < s.Length && char.IsDigit(s[start])) start++;
            if (start == numStart) return false;

            if (!double.TryParse(s.Substring(numStart, start - numStart), out double num))
                return false;

            while (start < s.Length && IsDelimiter(s[start])) start++;
            var unit = start < s.Length ? s.Substring(start).TrimEnd(')', ']', '}') : "";

            if (unit.Length == 0)
            {
                if (requireUnit) return false;   // a bare number ("30") says nothing
                value = TimeSpan.FromMilliseconds(num);   // "timeout:30": milliseconds by default
                return true;
            }

            var multiplier = ParseTimeUnit(unit);
            if (multiplier <= 0) return false;

            value = TimeSpan.FromMilliseconds(num * multiplier);
            return true;
        }

        // ms / milli* = milliseconds, s / sec* = seconds, m / min* = minutes; anything else is not a time.
        private static double ParseTimeUnit(string unit)
        {
            unit = unit.ToLowerInvariant();

            if (unit == "ms" || unit.StartsWith("milli")) return 1;
            if (unit == "s" || unit.StartsWith("sec")) return 1000;
            if (unit == "m" || unit.StartsWith("min")) return 60_000;

            return -1;
        }

        #endregion

        #region Condition Parsing

        private static readonly string[] ConditionKeywords = {
            "conditions", "condition", "cond", "on", "except", "if", "when", "for"
        };

        private static bool TryParseConditions(string tag, List<string> conditions)
        {
            foreach (var kw in ConditionKeywords)
            {
                if (!StartsWithIgnoreCase(tag, kw)) continue;

                var afterKw = tag.Substring(kw.Length);

                if (afterKw.Length > 0 && (afterKw[0] == 's' || afterKw[0] == 'S'))
                {
                    var next = afterKw.Length > 1 ? afterKw[1] : '\0';
                    if (next == '\0' || IsValueDelimiter(next))
                        afterKw = afterKw.Substring(1);
                }

                // Values come after a delimiter ("on:Timeout", "conditions[A,B]"): plain tags such as
                // @online, @format or @iffy are not conditions.
                if (afterKw.Length == 0 || !IsValueDelimiter(afterKw[0])) continue;

                var startCount = conditions.Count;
                ExtractMultipleValues(afterKw, conditions);
                return conditions.Count > startCount;
            }
            return false;
        }

        private static void ExtractMultipleValues(string s, List<string> result)
        {
            if (string.IsNullOrEmpty(s)) return;

            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && IsValueDelimiter(s[i])) i++;
                if (i >= s.Length) break;

                int start = i;
                while (i < s.Length && !IsValueDelimiter(s[i])) i++;

                if (i > start)
                    result.Add(s.Substring(start, i - start));
            }
        }

        private static bool IsValueDelimiter(char c) =>
            c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}' ||
            c == ',' || c == ':' || c == '=' || c == '_' || c == '-' || c == ' ' ||
            c == ';' || c == '|';

        #endregion

        #region Helpers

        private static bool IsDelimiter(char c) => c == '(' || c == ':' || c == '=' || c == '_' || c == '-' || c == '[';

        private static bool StartsWithIgnoreCase(string s, string prefix)
        {
            if (s.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (char.ToLowerInvariant(s[i]) != char.ToLowerInvariant(prefix[i]))
                    return false;
            return true;
        }

        private static bool MatchesAnyPrefix(string s, string[] prefixes)
        {
            foreach (var p in prefixes)
                if (StartsWithIgnoreCase(s, p))
                    return true;
            return false;
        }

        private static bool TryExtractNumber(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(s)) return false;

            int start = 0;
            while (start < s.Length && IsDelimiter(s[start])) start++;
            if (start == 0) return false;   // keyword + delimiter + number: "retry:3" yes, "retry3" no

            int end = start;
            while (end < s.Length && char.IsDigit(s[end])) end++;

            if (end > start)
                return int.TryParse(s.Substring(start, end - start), out value);

            return false;
        }

        #endregion
    }
}