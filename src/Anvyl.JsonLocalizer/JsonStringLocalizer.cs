using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Anvyl.JsonLocalizer
{
    /// <summary>
    /// Service that contains LocalizedStrings with json files as localization
    /// resources and a DistributedCache to cache out the values read from 
    /// json resource files
    /// </summary>
    public class JsonStringLocalizer : IStringLocalizer
    {
        #region Dependency Injection Fields

        private readonly IDistributedCache _cache;
        private readonly IOptions<JsonLocalizerOptions> _options;

        #endregion

        #region Private Fields

        private readonly JsonSerializer _serializer = new JsonSerializer();

        #endregion

        #region Constructors

        /// <summary>
        /// The default constructor for this servce injecting
        /// the required dependencies
        /// </summary>
        /// <param name="cache">The <see cref="IDistributedCache"/> implementation to use</param>
        /// <param name="options">The configuration options for the json localizer</param>
        public JsonStringLocalizer(IDistributedCache cache, IOptions<JsonLocalizerOptions> options)
        {
            _cache = cache;
            _options = options;
        }

        #endregion

        #region IStringLocalizer Implementation

        /// <summary>Gets the string resource with the given name.</summary>
        /// <param name="name">The name of the string resource.</param>
        /// <returns>
        /// The string resource as a <see cref="T:Microsoft.Extensions.Localization.LocalizedString" />.
        /// </returns>
        public LocalizedString this[string name]
        {
            get
            {
                var value = GetString(name);
                return new LocalizedString(name, value ?? $"[{name}]", value == null);
            }
        }

        /// <summary>
        /// Gets the string resource with the given name and formatted with the supplied arguments.
        /// </summary>
        /// <param name="name">The name of the string resource.</param>
        /// <param name="arguments">The values to format the string with.</param>
        /// <returns>
        /// The formatted string resource as a <see cref="T:Microsoft.Extensions.Localization.LocalizedString" />.
        /// </returns>
        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var actualValue = this[name];
                return !actualValue.ResourceNotFound
                    ? new LocalizedString(name, string.Format(actualValue.Value, arguments), false)
                    : actualValue;
            }
        }

        /// <summary>Gets all string resources.</summary>
        /// <param name="includeParentCultures">
        /// A <see cref="T:System.Boolean" /> indicating whether to include strings from parent cultures.
        /// </param>
        /// <returns>The strings.</returns>
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var files = System.IO.Directory.EnumerateFiles(_options.Value.ResourcesPath,
                $"*{CultureInfo.CurrentCulture.Name}*");

            return files
                .Select(GetSingleResourceBatch)
                .Aggregate((left, right) => left.Concat(right));
        }

        private IEnumerable<LocalizedString> GetSingleResourceBatch(string filePath)
        {
            using (var str = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sReader = new StreamReader(str))
            using (var reader = new JsonTextReader(sReader))
            {
                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    var key = (string)reader.Value;
                    reader.Read();
                    var value = _serializer.Deserialize<string>(reader);
                    yield return new LocalizedString(key, value, false);
                }
            }
        }

        /// <summary>
        /// Creates a new <see cref="T:Microsoft.Extensions.Localization.IStringLocalizer" /> for 
        /// a specific <see cref="T:System.Globalization.CultureInfo" />.
        /// </summary>
        /// <param name="culture">The <see cref="T:System.Globalization.CultureInfo" /> to use.</param>
        /// <returns>
        /// A culture-specific <see cref="T:Microsoft.Extensions.Localization.IStringLocalizer" />.
        /// </returns>
        public IStringLocalizer WithCulture(CultureInfo culture)
        {
            CultureInfo.DefaultThreadCurrentCulture = culture;
            return new JsonStringLocalizer(_cache, _options);
        }

        #endregion

        #region Private Helper Methods

        private string GetString(string key)
        {
            var values = key.Split('.').Take(2).ToList();
            var (ns, loc) = Tuple.Create(values[0], values[1]);
            var relativeFilePath = $"{_options.Value.ResourcesPath}/{ns}.{CultureInfo.CurrentCulture.Name}.json";
            var fullFilePath = Path.GetFullPath(relativeFilePath);

            if (File.Exists(fullFilePath))
            {
                var cacheKey = $"{_options.Value.CacheKeyPrefix}_{key}";
                var cacheValue = _cache.GetString(cacheKey);
                if (!string.IsNullOrEmpty(cacheValue)) return cacheValue;

                var result = PullDeserialize<string>(loc, Path.GetFullPath(relativeFilePath));
                if (!string.IsNullOrEmpty(result))
                    _cache.SetString(cacheKey, result);

                return result;
            }

            WriteEmptyKeys(new CultureInfo("en-US"), fullFilePath, key);
            return string.Empty;
        }

        private void WriteEmptyKeys(CultureInfo sourceCulture, string fullFilePath, string key)
        {
            var values = key.Split('.').Take(2).ToList();
            var (ns, loc) = Tuple.Create(values[0], values[1]);
            var sourceFilePath = $"{_options.Value.ResourcesPath}/{ns}.{sourceCulture.Name}.json";

            using (var str = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var outStream = File.Create(fullFilePath))
            using (var sWriter = new StreamWriter(outStream))
            using (var writer = new JsonTextWriter(sWriter))
            using (var sReader = new StreamReader(str))
            using (var reader = new JsonTextReader(sReader))
            {
                writer.Formatting = Formatting.Indented;
                var jobj = JObject.Load(reader);
                writer.WriteStartObject();
                foreach (var property in jobj.Properties())
                {
                    writer.WritePropertyName(property.Name);
                    writer.WriteNull();
                }
                writer.WriteEndObject();
            }
        }

        /// <summary>
        /// This is used to deserialize only one specific value from
        /// the json without loading the entire object.
        /// </summary>
        /// <typeparam name="T">Type of the object to deserialize</typeparam>
        /// <param name="propertyName">Name of the property to get from json</param>
        /// <param name="filePath">The file path of the json resource file</param>
        /// <returns>Deserialized propert from the json</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        private T PullDeserialize<T>(string propertyName, string filePath)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            using (var str = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sReader = new StreamReader(str))
            using (var reader = new JsonTextReader(sReader))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName
                        && (string)reader.Value == propertyName)
                    {
                        reader.Read();
                        return _serializer.Deserialize<T>(reader);
                    }
                }
                return default(T);
            }
        }

        #endregion
    }
}