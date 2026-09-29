using NUnit.Framework;
using TripleTapGames.Foundation.Editor;
using UnityEditor;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGLoadingSceneSetupTests
    {
        [Test]
        public void ResolveNextSceneNamePreservesValidConfiguredScene()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(TTGLoadingSceneSetup.ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Menu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true)
            };

            Assert.That(TTGLoadingSceneSetup.ResolveNextSceneName("Game", scenes), Is.EqualTo("Game"));
        }

        [Test]
        public void ResolveNextSceneNameSelectsFirstEnabledGameOwnedScene()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(TTGLoadingSceneSetup.ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Disabled.unity", false),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/FlowGrid.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Sample.unity", true)
            };

            Assert.That(TTGLoadingSceneSetup.ResolveNextSceneName("Core", scenes), Is.EqualTo("FlowGrid"));
        }

        [Test]
        public void ResolveNextSceneNameReturnsEmptyWhenNoGameSceneIsEnabled()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(TTGLoadingSceneSetup.ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Disabled.unity", false)
            };

            Assert.That(TTGLoadingSceneSetup.ResolveNextSceneName("Core", scenes), Is.Empty);
        }
    }
}
