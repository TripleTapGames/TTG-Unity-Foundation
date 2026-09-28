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
        public void SetUp() => TTGAnalytics.ClearProviders();
        [TearDown]
        public void TearDown() => TTGAnalytics.ClearProviders();

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

        private sealed class Provider : ITTGAnalyticsProvider
        {
            private readonly bool throws;
            public string ProviderName => throws ? "Throwing" : "Good";
            public bool IsInitialized => true;
            public int Calls { get; private set; }
            public Provider(bool throws) => this.throws = throws;
            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
            {
                Calls++;
                if (throws) throw new InvalidOperationException();
            }
        }
    }
}
