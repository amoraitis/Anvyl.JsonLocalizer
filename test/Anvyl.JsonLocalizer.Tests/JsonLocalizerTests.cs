using System.Globalization;
using Xunit;
using System.Linq;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Caching.Distributed;

namespace Anvyl.JsonLocalizer.Tests
{
    public class JsonLocalizerTests : BaseTest
    {
        [Fact(DisplayName = "Show [<key>] whenever the <key> does not exist in json file")]
        public void Returns_Resource_NotFound_With_Square_Brackets()
        {
            const string locKey = "test.Hello Traveller";
            Assert.NotNull(_localizer);
            Assert.NotNull(_localizer[locKey]);
            Assert.True(_localizer[locKey].ResourceNotFound);
            Assert.Equal($"[{locKey}]", _localizer[locKey].Value);
        }

        [Fact(DisplayName = "Returns a resource from CurrentCulture named json file")]
        public void ReturnsResource()
        {
            const string locKey = "test.Hello";
            Assert.NotNull(_localizer);
            Assert.NotNull(_localizer[locKey]);
            Assert.True(!_localizer[locKey].ResourceNotFound);
            Assert.Equal("Noroc", _localizer[locKey].Value);
        }


        [Fact(DisplayName = "Creates a new json file with null values copied from default culture")]
        public void Creates_New_File_Copying_From_Default()
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("re-RO");
            const string locKey = "test.Hello";
            Assert.NotNull(_localizer);
            Assert.True(_localizer[locKey].ResourceNotFound);
            Assert.Equal($"[{locKey}]", _localizer[locKey]);

            // Cleanup
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        }

        [Fact(DisplayName = "Returns a resource that should be cached afterwards")]
        public void Returns_Resource_And_Is_Saved_In_Cache()
        {
            const string locKey = "test.Hello";
            Assert.NotNull(_localizer);
            Assert.NotNull(_localizer[locKey]);
            Assert.True(!_localizer[locKey].ResourceNotFound);
            Assert.Equal("Noroc", _localizer[locKey].Value);
            Assert.Equal("Noroc", _cache.GetString($"{CacheKeyPrefix}_{locKey}"));

            // Cleanup
            _cache.Remove($"{CacheKeyPrefix}_{locKey}");
        }

        [Fact(DisplayName = "If the key does not exist it should not be saved in cache")]
        public void Unexistent_Value_Is_Not_Saved_In_Cache()
        {
            const string locKey = "test.Hello Traveller";
            Assert.NotNull(_localizer);
            Assert.NotNull(_localizer[locKey]);
            Assert.True(_localizer[locKey].ResourceNotFound);
            Assert.Equal($"[{locKey}]", _localizer[locKey].Value);
            Assert.Null(_cache.GetString($"{CacheKeyPrefix}_{locKey}"));
        }

        [Fact(DisplayName = "Test GetAllStrings")]
        public void GetAllStrings()
        {
            var localizedStrings = _localizer.GetAllStrings().ToArray();
            Assert.All(localizedStrings, ls => Assert.False(ls.ResourceNotFound));
            var names = localizedStrings.Select(ls => ls.Name).ToArray();
            var values = localizedStrings.Select(ls => ls.Value).ToArray();
            Assert.Contains("Hello", names);
            Assert.Contains("Hello {0}", names);
            Assert.Contains("Noroc", values);
            Assert.Contains("Noroc {0}", values);
        }
    }
}