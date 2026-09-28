using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGAnalyticsTests
    {
        [SetUp]
        public void SetUp()
        {
            TTGAnalytics.ClearProviders();
            PlayerPrefs.DeleteKey(TTGAnalytics.FirstLoginDateKey);
            PlayerPrefs.DeleteKey(TTGAnalytics.LastRetentionDayKey);
            foreach (var level in new[] { 5, 10, 15, 20 })
                PlayerPrefs.DeleteKey("TTG.Foundation.Analytics.MilestoneLevel." + level);
        }
        [TearDown]
        public void TearDown() => SetUp();

        [Test]
        public void ProviderFailureDoesNotStopFanOut()
        {
            var good = new Provider(false);
            TTGAnalytics.RegisterProvider(new Provider(true));
            TTGAnalytics.RegisterProvider(good);
            LogAssert.Expect(LogType.Error, "[TTG:Analytics] Throwing rejected an event. (InvalidOperationException)");
            TTGAnalytics.LogEvent("test_event");
            Assert.That(good.Calls, Is.EqualTo(1));
        }

        [Test]
        public void RetentionUsesUtcDayAndOnlySendsOncePerProvider()
        {
            var first = new Provider(false);
            TTGAnalytics.RegisterProvider(first);
            TTGAnalytics.RecordSessionRetention(new DateTime(2026, 9, 28, 23, 0, 0, DateTimeKind.Utc));
            TTGAnalytics.RecordSessionRetention(new DateTime(2026, 9, 28, 23, 30, 0, DateTimeKind.Utc));

            Assert.That(first.Events, Is.EqualTo(new[] { "day_0_retention" }));

            var late = new Provider(false, "Late");
            TTGAnalytics.RegisterProvider(late);
            Assert.That(late.Events, Is.EqualTo(new[] { "day_0_retention" }));
        }

        [Test]
        public void RetentionSendsNextUtcDayAfterFreshSession()
        {
            var first = new Provider(false);
            TTGAnalytics.RegisterProvider(first);
            TTGAnalytics.RecordSessionRetention(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc));
            TTGAnalytics.ClearProviders();

            var nextSession = new Provider(false);
            TTGAnalytics.RegisterProvider(nextSession);
            TTGAnalytics.RecordSessionRetention(new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(nextSession.Events, Is.EqualTo(new[] { "day_1_retention" }));
        }

        [Test]
        public void MilestoneCompletionSendsSupportedLevelOnlyOnce()
        {
            var provider = new Provider(false);
            TTGAnalytics.RegisterProvider(provider);

            TTGAnalytics.MilestoneCompleted(4);
            TTGAnalytics.MilestoneCompleted(5);
            TTGAnalytics.MilestoneCompleted(5);

            Assert.That(provider.Events, Is.EqualTo(new[] { "level_5_completed" }));
        }

        private sealed class Provider : ITTGAnalyticsProvider
        {
            private readonly bool throws;
            private readonly string providerName;
            public string ProviderName => providerName ?? (throws ? "Throwing" : "Good");
            public bool IsInitialized => true;
            public int Calls { get; private set; }
            public readonly List<string> Events = new List<string>();
            public Provider(bool throws, string providerName = null)
            {
                this.throws = throws;
                this.providerName = providerName;
            }
            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
            {
                Calls++;
                Events.Add(eventName);
                if (throws) throw new InvalidOperationException();
            }
        }
    }
}
