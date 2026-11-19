namespace Ohirok.GenerationInterceptor.Infrastructure
{
    internal static class HashtagExtractor
    {
        public static string[] Extract(string text)
        {
            if (string.IsNullOrEmpty(text)) return [];

            var result = new List<string>();
            int i = 0, len = text.Length;

            while (i < len)
            {
                // A hashtag starts at a '#' that begins the line or follows whitespace,
                // and runs to the next whitespace or the end of the text.
                if (text[i] == '#'
                    && (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\t')
                    && i + 1 < len && text[i + 1] != ' ' && text[i + 1] != '\t')
                {
                    int start = i + 1, end = start;
                    while (end < len && text[end] != ' ' && text[end] != '\t') end++;
                    if (end > start) result.Add(text.Substring(start, end - start));
                    i = end;
                }
                else i++;
            }
            return [.. result];
        }
    }
}
