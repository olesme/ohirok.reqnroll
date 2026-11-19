using System.Text.Json;
using System.Text.Json.Serialization;
using Reqnroll.Configuration;
using Reqnroll.Generator.Plugins;
using Reqnroll.Plugins;

namespace Ohirok.PluginChains.Common
{
    /// <summary>
    /// Centralized configuration provider for custom Reqnroll plugins.
    /// Reads reqnroll.json once and provides typed sections to plugins.
    /// </summary>
    public static class PluginConfigProvider
    {
        private static readonly object _lock = new();
        private static bool _subscribed;
        private static string _cachedJson;
        private static readonly Dictionary<string, object> _cachedSections = [];

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Ensures PluginConfigProvider subscribes to load reqnroll.json once for RuntimePlugin.
        /// Safe to call from multiple plugins - subscribes only once.
        /// </summary>
        public static void Initialize(RuntimePluginEvents events)
        {
            lock (_lock)
            {
                if (_subscribed) return;

                events.CustomizeGlobalDependencies += (_, args) =>
                {
                    try
                    {
                        var locator = args.ObjectContainer.Resolve<IReqnrollJsonLocator>();
                        var path = locator?.GetReqnrollJsonFilePath();
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            _cachedJson = File.ReadAllText(path);
                        }
                    }
                    catch
                    {
                        // Silently fail - will use defaults
                        _cachedJson = null;
                    }
                };

                _subscribed = true;
            }
        }

        /// <summary>
        /// Ensures PluginConfigProvider subscribes to load reqnroll.json once for GeneratorPlugin.
        /// Safe to call from multiple plugins - subscribes only once.
        /// </summary>
        public static void Initialize(GeneratorPluginEvents events)
        {
            lock (_lock)
            {
                if (_subscribed) return;

                events.CustomizeDependencies += (_, args) =>
                {
                    try
                    {
                        var locator = args.ObjectContainer.Resolve<IReqnrollJsonLocator>();
                        var path = locator?.GetReqnrollJsonFilePath();
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            _cachedJson = File.ReadAllText(path);
                        }
                    }
                    catch
                    {
                        _cachedJson = null;
                    }
                };

                _subscribed = true;
            }
        }

        /// <summary>
        /// Loads reqnroll.json directly from a file path, bypassing the Reqnroll runtime/generator
        /// plugin hooks. For use by standalone tools (e.g. Ohirok.Reporting.Combine) that have no
        /// live Reqnroll host to resolve <see cref="IReqnrollJsonLocator"/> from.
        /// </summary>
        public static void LoadFromFile(string path)
        {
            lock (_lock)
            {
                _cachedJson = !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllText(path) : null;
                _cachedSections.Clear();
            }
        }

        /// <summary>
        /// Get plugin configuration section from reqnroll.json
        /// </summary>
        /// <typeparam name="TSection">Plugin configuration section type</typeparam>
        /// <param name="sectionName">JSON property name (e.g., "stepHashtags", "xfail")</param>
        /// <returns>Configuration section (from JSON or default)</returns>
        public static TSection GetSection<TSection>(string sectionName) where TSection : class, new()
        {
            if (string.IsNullOrEmpty(sectionName))
                throw new ArgumentNullException(nameof(sectionName));

            lock (_lock)
            {
                // Check if already cached
                if (_cachedSections.TryGetValue(sectionName, out var cached))
                    return (TSection)cached;

                // Try deserialize section
                var section = DeserializeSection<TSection>(sectionName) ?? new TSection();

                // Cache for future calls
                _cachedSections[sectionName] = section;
                return section;
            }
        }

        /// <summary>
        /// Get a single value from reqnroll.json by path
        /// </summary>
        /// <typeparam name="TValue">Value type</typeparam>
        /// <param name="path">JSON path (e.g., "myPlugin.enabled" or "globalTimeout")</param>
        /// <param name="defaultValue">Default value if not found or parsing fails</param>
        /// <returns>Value from JSON or default</returns>
        public static TValue GetValue<TValue>(string path, TValue defaultValue = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            lock (_lock)
            {
                var cacheKey = $"_value_{path}";
                if (_cachedSections.TryGetValue(cacheKey, out var cached))
                    return (TValue)cached;

                if (string.IsNullOrEmpty(_cachedJson))
                {
                    _cachedSections[cacheKey] = defaultValue;
                    return defaultValue;
                }

                var (found, value) = TryGetValueByPath<TValue>(path);
                var result = found ? value : defaultValue;

                _cachedSections[cacheKey] = result;
                return result;
            }
        }

        private static (bool found, TValue value) TryGetValueByPath<TValue>(string path)
        {
            try
            {
                using var doc = JsonDocument.Parse(_cachedJson);
                var element = doc.RootElement;

                foreach (var part in path.Split('.'))
                {
                    if (!element.TryGetProperty(part, out element))
                        return (false, default);
                }

                var json = element.GetRawText();
                var value = JsonSerializer.Deserialize<TValue>(json, _jsonOptions);
                return (true, value);
            }
            catch
            {
                return (false, default);
            }
        }

        /// <summary>
        /// Get raw JSON content from reqnroll.json
        /// </summary>
        /// <returns>Raw JSON string or null if not found</returns>
        public static string GetRawJson()
        {
            lock (_lock)
            {
                return _cachedJson;
            }
        }

        /// <summary>
        /// Clear all cached data (mainly for testing)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _cachedJson = null;
                _cachedSections.Clear();
                _subscribed = false;
            }
        }

        private static TSection DeserializeSection<TSection>(string sectionName) where TSection : class, new()
        {
            if (string.IsNullOrEmpty(_cachedJson)) return null;

            try
            {
                // Parse as JsonDocument to extract specific section
                using var doc = JsonDocument.Parse(_cachedJson);

                if (!doc.RootElement.TryGetProperty(sectionName, out var sectionElement))
                    return null;

                var json = sectionElement.GetRawText();
                return JsonSerializer.Deserialize<TSection>(json, _jsonOptions);
            }
            catch (Exception)
            {
                // Return null to use default
                return null;
            }
        }
    }
}
