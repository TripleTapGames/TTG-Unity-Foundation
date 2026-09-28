using System;
using System.Collections.Generic;
using GameAnalyticsSDK;
using GameAnalyticsSDK.Setup;
using NUnit.Framework;
using TripleTapGames.Foundation.Adapters.GameAnalytics;
using UnityEngine;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGGameAnalyticsConfigurationTests
    {
        private Settings settings;
        private TTGGameAnalyticsConfig config;
        [SetUp] public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<Settings>();
            config = new TTGGameAnalyticsConfig { AndroidGameKey = "fixture-android", AndroidSecretKey = "fixture-android-secret", IosGameKey = "fixture-ios", IosSecretKey = "fixture-ios-secret" };
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(settings);
        [Test] public void MapsBothPlatformsAndDoesNotDuplicateOnRepeat()
        {
            Assert.That(TTGGameAnalyticsConfiguration.Apply(config, settings), Is.True);
            Check(RuntimePlatform.Android, config.AndroidGameKey, config.AndroidSecretKey);
            Check(RuntimePlatform.IPhonePlayer, config.IosGameKey, config.IosSecretKey);
            Assert.That(TTGGameAnalyticsConfiguration.Apply(config, settings), Is.False);
            Assert.That(settings.Platforms.Count, Is.EqualTo(2));
        }
        [Test] public void PreservesOtherPlatformsAndBuildSettings()
        {
            settings.AddPlatform(RuntimePlatform.WindowsPlayer);
            settings.UpdateGameKey(0, "fixture-other"); settings.UpdateSecretKey(0, "fixture-other-secret");
            settings.AddPlatform(RuntimePlatform.IPhonePlayer); settings.Build[1] = "2.7";
            TTGGameAnalyticsConfiguration.Apply(config, settings);
            Check(RuntimePlatform.WindowsPlayer, "fixture-other", "fixture-other-secret");
            Assert.That(settings.Build[1], Is.EqualTo("2.7"));
            Check(RuntimePlatform.IPhonePlayer, config.IosGameKey, config.IosSecretKey);
        }
        [Test] public void ReplacesExistingKeysIncludingSwaps()
        {
            TTGGameAnalyticsConfiguration.Apply(config, settings);
            var oldKey = config.AndroidGameKey; var oldSecret = config.AndroidSecretKey;
            config.AndroidGameKey = config.IosGameKey; config.AndroidSecretKey = config.IosSecretKey;
            config.IosGameKey = oldKey; config.IosSecretKey = oldSecret;
            TTGGameAnalyticsConfiguration.Apply(config, settings);
            Check(RuntimePlatform.Android, config.AndroidGameKey, config.AndroidSecretKey);
            Check(RuntimePlatform.IPhonePlayer, oldKey, oldSecret);
        }
        [Test] public void EmptyValuesClearStaleKeys()
        {
            TTGGameAnalyticsConfiguration.Apply(config, settings);
            config.IosGameKey = null; config.IosSecretKey = " ";
            TTGGameAnalyticsConfiguration.Apply(config, settings);
            Check(RuntimePlatform.IPhonePlayer, "", "");
        }
        [Test] public void DuplicateKeysFailWithoutChangingSettingsOrLeakingValues()
        {
            TTGGameAnalyticsConfiguration.Apply(config, settings);
            config.IosGameKey = config.AndroidGameKey;
            var error = Assert.Throws<InvalidOperationException>(() => TTGGameAnalyticsConfiguration.Apply(config, settings));
            Assert.That(error.Message, Does.Not.Contain(config.AndroidGameKey));
            Check(RuntimePlatform.IPhonePlayer, "fixture-ios", "fixture-ios-secret");
        }
        [Test] public void ConflictsWithOtherPlatformsDoNotMutateSettings()
        {
            settings.AddPlatform(RuntimePlatform.WindowsPlayer);
            settings.UpdateGameKey(0, config.AndroidGameKey);
            Assert.Throws<InvalidOperationException>(() => TTGGameAnalyticsConfiguration.Apply(config, settings));
            Assert.That(settings.Platforms.Count, Is.EqualTo(1));
        }
        [TestCase(TTGEventNames.LevelStart, GAProgressionStatus.Start)]
        [TestCase(TTGEventNames.LevelComplete, GAProgressionStatus.Complete)]
        [TestCase(TTGEventNames.LevelFail, GAProgressionStatus.Fail)]
        public void MapsTtgLevelEventsToGameAnalyticsProgression(string eventName, GAProgressionStatus expected)
        {
            var parameters = new Dictionary<string, object> { { "level_id", "Level_7" } };
            Assert.That(GameAnalyticsService.TryGetProgression(eventName, parameters, out var status, out var level), Is.True);
            Assert.That(status, Is.EqualTo(expected));
            Assert.That(level, Is.EqualTo("Level_7"));
        }
        [Test] public void NonLevelEventRemainsDesignEvent()
        {
            Assert.That(GameAnalyticsService.TryGetProgression("button_clicked",
                new Dictionary<string, object> { { "level_id", "Level_7" } }, out _, out _), Is.False);
        }
        private void Check(RuntimePlatform platform, string key, string secret)
        {
            var i = settings.Platforms.IndexOf(platform);
            Assert.That(i, Is.GreaterThanOrEqualTo(0));
            Assert.That(settings.GetGameKey(i), Is.EqualTo(key));
            Assert.That(settings.GetSecretKey(i), Is.EqualTo(secret));
        }
    }
}
