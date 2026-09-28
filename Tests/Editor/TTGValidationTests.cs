using NUnit.Framework;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGValidationTests
    {
        [Test]
        public void ValidationResultKeepsCategoryMessageAndSeverity()
        {
            var result = new Editor.TTGValidationResult("Firebase", "Missing file", Editor.TTGValidationSeverity.Error);
            Assert.That(result.Category, Is.EqualTo("Firebase"));
            Assert.That(result.Message, Is.EqualTo("Missing file"));
            Assert.That(result.Severity, Is.EqualTo(Editor.TTGValidationSeverity.Error));
        }
    }
}
