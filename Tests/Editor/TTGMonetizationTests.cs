using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGMonetizationTests
    {
        [TearDown]
        public void Cleanup()
        {
            TTGAdRemoteConfig.ClearReader();
            TTGAds.Configure(null, null);
            TTGAds.Clock = () => Time.realtimeSinceStartupAsDouble;
            TTGIAP.Configure(null);
        }

        [Test]
        public void RemoteThresholdAppliesToReadinessAndShowWithoutMutatingAsset()
        {
            var config = ScriptableObject.CreateInstance<TTAdsConfig>();
            try
            {
                config.AdsEnabled = true;
                config.Interstitial.LevelInterval = 2;
                var provider = new Ads();
                TTGAds.Clock = () => 100;
                TTGAds.Configure(provider, config);
                TTGAdRemoteConfig.SetReader(key => key == "AdLevelStart" ? "15" : null);
                TTGAds.NotifyLevelCompleted(14);
                Assert.That(TTGAds.IsInterstitialReady, Is.False);
                TTGAdResult result = null;
                TTGAds.ShowInterstitial(TTGAdPlacement.LevelComplete, value => result = value);
                Assert.That(result.Status, Is.EqualTo(TTGAdResultStatus.BlockedByRules));
                TTGAds.NotifyLevelCompleted(15);
                Assert.That(TTGAds.IsInterstitialReady, Is.True);
                TTGAds.ShowInterstitial(TTGAdPlacement.LevelComplete);
                Assert.That(provider.Shown, Is.EqualTo(1));
                Assert.That(config.Interstitial.LevelInterval, Is.EqualTo(2));
                TTGAdRemoteConfig.SetReader(key => key == "interstitial_enabled" ? "false" : null);
                Assert.That(TTGAds.IsInterstitialReady, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void InvalidOrUnavailableRemoteValuesPreserveLocalRules()
        {
            var local = new TTGInterstitialRules { LevelInterval = 3, CooldownSeconds = 20, MinimumSessionTime = 5 };
            var resolved = TTGAdRemoteConfig.Resolve(local, key => key == "AdCoolDown" ? "NaN" : "-1");
            Assert.That(resolved.LevelInterval, Is.EqualTo(3));
            Assert.That(resolved.CooldownSeconds, Is.EqualTo(20));
            Assert.That(resolved.MinimumSessionTime, Is.EqualTo(5));
            Assert.That(TTGAdRemoteConfig.Resolve(local, key => { throw new Exception(); }), Is.SameAs(local));
            Assert.That(TTGAdRemoteConfig.Resolve(local, key => null).LevelInterval, Is.EqualTo(3));
        }

        [Test]
        public void CatalogRejectsEmptyWhitespaceAndDuplicateIdsWithoutChangingCase()
        {
            Assert.That(TTGIAPCatalog.Validate(new List<TTGIAPProduct>()), Is.Not.Null);
            var products = new List<TTGIAPProduct> { new TTGIAPProduct { ProductId = " noads" } };
            Assert.That(TTGIAPCatalog.Validate(products), Is.Not.Null);
            products[0].ProductId = "noads";
            Assert.That(TTGIAPCatalog.Validate(products), Is.Null);
            products.Add(new TTGIAPProduct { ProductId = "noads" });
            Assert.That(TTGIAPCatalog.Validate(products), Does.Contain("Duplicate"));
            products[1].ProductId = "noAds";
            Assert.That(TTGIAPCatalog.Validate(products), Is.Null, "IDs remain case-sensitive, not silently normalized.");
        }

        [Test]
        public void OwnershipIsUnknownUntilStoreReadyAndUsesExactId()
        {
            Assert.That(TTGIAP.HasReceipt("noads"), Is.Null);
            var store = new Store();
            TTGIAP.Configure(store);
            Assert.That(TTGIAP.HasReceipt("noads"), Is.True);
            Assert.That(TTGIAP.HasReceipt("noAds"), Is.Null);
            int received = 0;
            Action<TTGPurchaseResult> listener = value => received++;
            TTGIAP.PurchaseReceived += listener;
            try { TTGIAP.NotifyPurchaseReceived(new TTGPurchaseResult(TTGPurchaseStatus.Succeeded, "noads")); }
            finally { TTGIAP.PurchaseReceived -= listener; }
            Assert.That(received, Is.EqualTo(1), "Restored purchases can reach game code without a pending button callback.");
        }

        [Test]
        public void MissingStoreAndProductReturnVisibleReasons()
        {
            TTGIAP.Configure(null);
            TTGPurchaseResult result = null;
            TTGIAP.Purchase("noads", value => result = value);
            Assert.That(result.Status, Is.EqualTo(TTGPurchaseStatus.NotInitialized));
            Assert.That(result.Message, Is.Not.Empty);
            TTGIAP.Purchase(" noads", value => result = value);
            Assert.That(result.Status, Is.EqualTo(TTGPurchaseStatus.ProductUnavailable));
        }

        [Test]
        public void StartupSummaryIncludesServiceReasonAndException()
        {
            var report = new TTGInitializationReport(new List<TTGInitializationResult>
            {
                TTGInitializationResult.Successful("Ads"),
                new TTGInitializationResult("Unity IAP", TTGInitializationStatus.Failure,
                    "NoProductsAvailable: No product returned from the store.", new Exception("test detail"))
            }, TTGInitializationStatus.Failure);
            Assert.That(report.FailureSummary, Does.Contain("Unity IAP: NoProductsAvailable"));
            Assert.That(report.FailureSummary, Does.Contain("test detail"));
            Assert.That(report.FailureSummary, Does.Not.Contain("Ads"));
        }

        private sealed class Store : ITTGIAPProvider, ITTGIAPOwnershipProvider
        {
            public bool IsInitialized => true;
            public bool? HasReceipt(string id) => id == "noads" ? true : (bool?)null;
            public void Purchase(string id, Action<TTGPurchaseResult> callback) => callback(new TTGPurchaseResult(TTGPurchaseStatus.Succeeded, id));
            public void RestorePurchases(Action<bool> callback) => callback(true);
        }
        private sealed class Ads : ITTGAdsProvider
        {
            public int Shown;
            public bool IsInterstitialReady => true;
            public bool IsRewardedReady => true;
            public void ShowInterstitial(string id, TTGAdPlacement placement, Action<TTGAdResult> callback) { Shown++; callback(new TTGAdResult(TTGAdResultStatus.Closed)); }
            public void ShowRewarded(string id, TTGAdPlacement placement, Action reward, Action<TTGAdResult> close) { }
            public void ShowBanner(string id, TTGBannerPosition position) { }
            public void HideBanner(string id) { }
            public void ShowMrec(string id) { }
            public void HideMrec(string id) { }
        }
    }
}
