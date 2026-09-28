using NUnit.Framework;
using UnityEngine;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGConfigTests
    {
        [Test]
        public void AdsConfigSelectsExpectedPlatformUnits()
        {
            var config = ScriptableObject.CreateInstance<TTAdsConfig>();
            config.Android.InterstitialId = "android";
            config.IOS.InterstitialId = "ios";
            Assert.That(config.GetPlatformUnits(RuntimePlatform.Android).InterstitialId, Is.EqualTo("android"));
            Assert.That(config.GetPlatformUnits(RuntimePlatform.IPhonePlayer).InterstitialId, Is.EqualTo("ios"));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void RetryDelayIsBoundedExponential()
        {
            Assert.That(TTGAds.GetRetryDelaySeconds(1), Is.EqualTo(2));
            Assert.That(TTGAds.GetRetryDelaySeconds(3), Is.EqualTo(8));
            Assert.That(TTGAds.GetRetryDelaySeconds(100), Is.EqualTo(64));
        }

        [Test]
        public void CallbackGuardInvokesAtMostOnce()
        {
            var calls = 0;
            var once = new TTGAds.Once(() => calls++);
            once.Invoke();
            once.Invoke();
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
