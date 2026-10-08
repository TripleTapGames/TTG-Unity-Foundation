using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGLevelProgressTests
    {
        private readonly List<string> tracks = new List<string>();

        [SetUp]
        public void SetUp()
        {
            TTGAnalytics.ClearProviders();
            TTGAds.Configure(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < tracks.Count; i++) TTGLevelProgress.Reset(tracks[i]);
            tracks.Clear();
            TTGAnalytics.ClearProviders();
            TTGAds.Configure(null, null);
        }

        [Test]
        public void NewTrackStartsAtLevelOneAndPersistsMutations()
        {
            var track = NewTrack();
            var initial = TTGLevelProgress.GetSnapshot(track);

            Assert.That(initial.HighestUnlockedLevel, Is.EqualTo(1));
            Assert.That(initial.Levels, Is.Empty);
            Assert.That(TTGLevelProgress.IsUnlocked(1, track), Is.True);
            Assert.That(TTGLevelProgress.IsUnlocked(2, track), Is.False);

            TTGLevelProgress.RecordAttempt(1, "level_001", track);
            TTGLevelProgress.RecordCompletion(1, "level_001", 2, 100, track);

            var restored = TTGLevelProgress.GetSnapshot(track);
            Assert.That(restored.HighestUnlockedLevel, Is.EqualTo(2));
            Assert.That(restored.Levels, Has.Count.EqualTo(1));
            Assert.That(restored.Levels[0].AttemptCount, Is.EqualTo(1));
            Assert.That(restored.Levels[0].CompletionCount, Is.EqualTo(1));
        }

        [Test]
        public void OutOfOrderCompletionAdvancesUnlockWithoutReducingItLater()
        {
            var track = NewTrack();
            TTGLevelProgress.RecordCompletion(5, "level_005", trackId: track);
            TTGLevelProgress.RecordCompletion(2, "level_002", trackId: track);

            Assert.That(TTGLevelProgress.GetSnapshot(track).HighestUnlockedLevel, Is.EqualTo(6));
            Assert.That(TTGLevelProgress.IsUnlocked(6, track), Is.True);
            Assert.That(TTGLevelProgress.IsUnlocked(7, track), Is.False);
        }

        [Test]
        public void StableIdKeepsResultsWhenNumericPositionChanges()
        {
            var track = NewTrack();
            TTGLevelProgress.RecordCompletion(3, "stable-level", 2, 400, track);
            TTGLevelProgress.RecordAttempt(8, "stable-level", track);

            var record = TTGLevelProgress.GetLevel("stable-level", track);
            Assert.That(record.LastKnownLevelNumber, Is.EqualTo(8));
            Assert.That(record.IsCompleted, Is.True);
            Assert.That(record.BestStars, Is.EqualTo(2));
            Assert.That(record.BestScore, Is.EqualTo(400));
        }

        [Test]
        public void ReplaysKeepBestResultAndCountAttemptsAndCompletionsSeparately()
        {
            var track = NewTrack();
            TTGLevelProgress.RecordAttempt(1, "level_001", track);
            TTGLevelProgress.RecordAttempt(1, "level_001", track);
            TTGLevelProgress.RecordCompletion(1, "level_001", 3, 500, track);
            TTGLevelProgress.RecordCompletion(1, "level_001", 1, 200, track);

            var record = TTGLevelProgress.GetLevel("level_001", track);
            Assert.That(record.AttemptCount, Is.EqualTo(2));
            Assert.That(record.CompletionCount, Is.EqualTo(2));
            Assert.That(record.BestStars, Is.EqualTo(3));
            Assert.That(record.BestScore, Is.EqualTo(500));
        }

        [Test]
        public void NamedTracksAreIsolatedAndResetOnlyDeletesRequestedTrack()
        {
            var campaign = NewTrack();
            var daily = NewTrack();
            TTGLevelProgress.RecordCompletion(4, "campaign_004", trackId: campaign);
            TTGLevelProgress.RecordCompletion(2, "daily_002", trackId: daily);

            TTGLevelProgress.Reset(campaign);

            Assert.That(TTGLevelProgress.GetSnapshot(campaign).HighestUnlockedLevel, Is.EqualTo(1));
            Assert.That(TTGLevelProgress.GetSnapshot(daily).HighestUnlockedLevel, Is.EqualTo(3));
        }

        [Test]
        public void SeedImportsOnceAndCompletedRecordsAdvanceUnlock()
        {
            var track = NewTrack();
            var seed = new TTGLevelProgressSeed(3, new[]
            {
                new TTGLevelProgressSeedRecord("legacy_004", 4, 2, 1, 2, 900)
            });

            Assert.That(TTGLevelProgress.TrySeed(seed, track), Is.True);
            Assert.That(TTGLevelProgress.TrySeed(new TTGLevelProgressSeed(20), track), Is.False);

            var snapshot = TTGLevelProgress.GetSnapshot(track);
            Assert.That(snapshot.HighestUnlockedLevel, Is.EqualTo(5));
            Assert.That(snapshot.Levels[0].BestScore, Is.EqualTo(900));
        }

        [Test]
        public void CorruptSaveLogsAndFallsBackToFreshProgress()
        {
            var track = NewTrack();
            PlayerPrefs.SetString(TTGLevelProgress.GetStorageKey(track), "not-json");
            PlayerPrefs.Save();

            LogAssert.Expect(LogType.Warning,
                "[TTG:Progress] Saved level progress for track '" + track + "' is invalid. Fresh progress will be used.");
            var snapshot = TTGLevelProgress.GetSnapshot(track);

            Assert.That(snapshot.HighestUnlockedLevel, Is.EqualTo(1));
            Assert.That(snapshot.Levels, Is.Empty);
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            var track = NewTrack();
            Assert.Throws<ArgumentOutOfRangeException>(() => TTGLevelProgress.RecordAttempt(0, "level", track));
            Assert.Throws<ArgumentException>(() => TTGLevelProgress.RecordAttempt(1, " ", track));
            Assert.Throws<ArgumentException>(() => TTGLevelProgress.GetSnapshot(" "));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TTGLevelProgress.RecordCompletion(1, "level", -1, trackId: track));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TTGLevelProgress.RecordCompletion(1, "level", score: -1, trackId: track));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TTGLevelProgress.TrySeed(new TTGLevelProgressSeed(0), track));
            Assert.Throws<ArgumentException>(() => TTGLevelProgress.TrySeed(
                new TTGLevelProgressSeed(1, new[]
                {
                    new TTGLevelProgressSeedRecord("duplicate", 1),
                    new TTGLevelProgressSeedRecord("duplicate", 2)
                }), track));
        }

        [Test]
        public void ProgressDoesNotEmitAnalyticsOrUpdateAdGating()
        {
            var track = NewTrack();
            var analytics = new AnalyticsProvider();
            var ads = new AdsProvider();
            var config = ScriptableObject.CreateInstance<TTAdsConfig>();
            config.AdsEnabled = true;
            config.Interstitial.Enabled = true;
            config.Interstitial.LevelInterval = 1;
            config.Interstitial.MinimumSessionTime = 0;
            config.Interstitial.CooldownSeconds = 0;
            TTGAnalytics.RegisterProvider(analytics);
            TTGAds.Configure(ads, config);

            TTGLevelProgress.RecordAttempt(1, "level_001", track);
            TTGLevelProgress.RecordCompletion(1, "level_001", trackId: track);

            Assert.That(analytics.Calls, Is.Zero);
            Assert.That(TTGAds.IsInterstitialReady, Is.False);
            UnityEngine.Object.DestroyImmediate(config);
        }

        private string NewTrack()
        {
            var track = "test-" + Guid.NewGuid().ToString("N");
            tracks.Add(track);
            return track;
        }

        private sealed class AnalyticsProvider : ITTGAnalyticsProvider
        {
            public string ProviderName => "ProgressTest";
            public bool IsInitialized => true;
            public int Calls { get; private set; }
            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null) => Calls++;
        }

        private sealed class AdsProvider : ITTGAdsProvider
        {
            public bool IsInterstitialReady => true;
            public bool IsRewardedReady => true;
            public void ShowInterstitial(string adUnitId, TTGAdPlacement placement, Action<TTGAdResult> completed) { }
            public void ShowRewarded(string adUnitId, TTGAdPlacement placement, Action rewarded,
                Action<TTGAdResult> closed) { }
            public void ShowBanner(string adUnitId, TTGBannerPosition position) { }
            public void HideBanner(string adUnitId) { }
            public void ShowMrec(string adUnitId) { }
            public void HideMrec(string adUnitId) { }
        }
    }
}
