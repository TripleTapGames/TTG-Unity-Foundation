using NUnit.Framework;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGManifestRegistryTests
    {
        [Test]
        public void AddsRegistryAndDoesNotDuplicateIt()
        {
            const string original = "{\n  \"dependencies\": {}\n}";
            var once = Editor.TTGManifestRegistryUtility.EnsureRegistry(original, "Test", "https://registry.example", "com.example");
            var twice = Editor.TTGManifestRegistryUtility.EnsureRegistry(once, "Test", "https://registry.example", "com.example");
            StringAssert.Contains("https://registry.example", once);
            Assert.That(twice, Is.EqualTo(once));
        }

        [Test]
        public void AddsMissingScopeToExistingRegistry()
        {
            const string original = "{\"dependencies\":{},\"scopedRegistries\":[{\"name\":\"OpenUPM\",\"url\":\"https://package.openupm.com\",\"scopes\":[\"com.google\"]}]}";
            var updated = Editor.TTGManifestRegistryUtility.EnsureRegistry(original, "OpenUPM", "https://package.openupm.com", "com.gameanalytics");
            StringAssert.Contains("com.gameanalytics", updated);
        }
    }
}
