using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reqnroll.Configuration;
using Reqnroll.Generator.Plugins;
using Reqnroll.Plugins;

namespace Ohirok.PluginChains.Common
{
    /// <summary>
    /// Centralized configuration provider for custom Reqnroll plugins: reads the project's reqnroll.json and provides
    /// typed sections to plugins.
    /// <para>
    /// It reloads for every Reqnroll host (each <see cref="GeneratorPluginEvents"/> / <see cref="RuntimePluginEvents"/>
    /// instance, that is, each project being generated or each test run), because Reqnroll's generator can build
    /// several projects in one process: under Visual Studio's MSBuild the plugin assemblies, and so these statics,
    /// stay loaded from one project to the next. Do not cache a section in a static field of your own; call
    /// <see cref="GetSection{TSection}"/>, which caches it until the next reload.
    /// </para>
    /// </summary>
    public static class PluginConfigProvider
    {
        private static readonly object _lock = new();
        private static readonly ConditionalWeakTable<object, object> _subscribed = new();
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
        /// Loads the test project's reqnroll.json when Reqnroll builds its global container. Safe to call from every
        /// plugin: it subscribes once per <paramref name="events"/> instance.
        /// </summary>
        public static void Initialize(RuntimePluginEvents events)
        {
            if (!FirstCallFor(events)) return;
            events.CustomizeGlobalDependencies += (_, args) => Load(args.ObjectContainer);
        }

        /// <summary>
        /// Loads the project's reqnroll.json before the generator plugins customize the generator container. Safe to
        /// call from every plugin: it subscribes once per <paramref name="events"/> instance, so once per project.
        /// </summary>
        public static void Initialize(GeneratorPluginEvents events)
        {
            if (!FirstCallFor(events)) return;
            events.CustomizeDependencies += (_, args) => Load(args.ObjectContainer);
        }

        private static bool FirstCallFor(object events)
        {
            lock (_lock)
            {
                if (_subscribed.TryGetValue(events, out _)) return false;
                _subscribed.Add(events, events);
                return true;
            }
        }

        private static void Load(Reqnroll.BoDi.IObjectContainer container)
        {
            string json = null;
            try
            {
                var path = container.Resolve<IReqnrollJsonLocator>()?.GetReqnrollJsonFilePath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) json = File.ReadAllText(path);
            }
            catch
            {
                // No readable reqnroll.json: every section uses its defaults.
            }

            lock (_lock)
            {
                _cachedJson = json;
                _cachedSections.Clear();
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
