using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;

namespace TripleTapGames.Foundation.Editor
{
    public static class TTGSetupValidator
    {
        public static IReadOnlyList<TTGValidationResult> Validate(BuildTarget target)
        {
            var results = new List<TTGValidationResult>();
            var config = AssetDatabase.LoadAssetAtPath<TTGProjectConfig>(TTGConfigAssetUtility.ProjectConfigPath);
            var ads = AssetDatabase.LoadAssetAtPath<TTAdsConfig>(TTGConfigAssetUtility.AdsConfigPath);
            if (config == null)
            {
                Error(results, "Project", "TTGProjectConfig is missing. Create it from the TTG Setup window.");
                return results;
            }

            if (string.IsNullOrWhiteSpace(PlayerSettings.GetApplicationIdentifier(BuildPipeline.GetBuildTargetGroup(target))))
                Error(results, "Project", "The active target has no bundle identifier.");

            ValidateDependencies(results);
            ValidateFacebook(config, results);
            ValidateFirebase(config, target, results);
            ValidateGameAnalytics(config, target, results);
            ValidateAds(config, ads, target, results);
            ValidateSingular(config, results);
            ValidateIap(config, results);

            if (!File.Exists(Path.Combine(TTGConfigAssetUtility.GeneratedRoot, ".gitignore")))
                Warning(results, "Security", "Generated local config is not protected by its folder .gitignore.");
            if (!TTGPrivacy.HasResolvedConsent && (config.Facebook.Enabled || config.Firebase.Enabled || config.AppLovin.Enabled || config.Singular.Enabled))
                Warning(results, "Privacy", "Consent has not been supplied in this editor profile; consent-dependent services will remain deferred.");
            if (!results.Any(item => item.Severity == TTGValidationSeverity.Error))
                results.Add(new TTGValidationResult("Project", "No blocking configuration errors found.", TTGValidationSeverity.Info));
            return results;
        }

        private static void ValidateDependencies(List<TTGValidationResult> results)
        {
            foreach (var dependency in TTGDependencyCatalog.All)
            {
                if (!dependency.IsInstalled()) Warning(results, "Dependencies", dependency.Name + " " + dependency.Version + " is not installed.");
            }

            var hasLegacyGa = Directory.Exists("Assets/GameAnalytics");
            var hasPackageGa = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == "com.gameanalytics.sdk");
            if (hasLegacyGa && hasPackageGa)
                Error(results, "GameAnalytics", "Both the legacy Assets import and UPM package are present. Remove one installation method.");
            else if (hasLegacyGa)
                Warning(results, "GameAnalytics", "Legacy Assets import detected. The TTG adapter requires the approved UPM assembly before it can be enabled.");
        }

        private static void ValidateFacebook(TTGProjectConfig config, List<TTGValidationResult> results)
        {
            if (!config.Facebook.Enabled) return;
            if (!File.Exists("Assets/FacebookSDK/Plugins/Facebook.Unity.dll")) Error(results, "Facebook", "Facebook SDK is enabled but unavailable.");
            if (string.IsNullOrWhiteSpace(config.Facebook.AppId)) Error(results, "Facebook", "Facebook App ID is missing.");
        }

        private static void ValidateFirebase(TTGProjectConfig config, BuildTarget target, List<TTGValidationResult> results)
        {
            TTGFirebaseSetup.Validate(config.Firebase.Enabled, target, TTGFirebaseSetup.Inspect(),
                TTGFirebaseSetup.AdapterIsCurrent, File.Exists, results);
        }

        private static void ValidateGameAnalytics(TTGProjectConfig config, BuildTarget target, List<TTGValidationResult> results)
        {
            if (!config.GameAnalytics.Enabled) return;
            var key = target == BuildTarget.iOS ? config.GameAnalytics.IosGameKey : config.GameAnalytics.AndroidGameKey;
            var secret = target == BuildTarget.iOS ? config.GameAnalytics.IosSecretKey : config.GameAnalytics.AndroidSecretKey;
            if (string.IsNullOrWhiteSpace(key)) Error(results, "GameAnalytics", "The active platform Game Key is missing.");
            if (string.IsNullOrWhiteSpace(secret)) Error(results, "GameAnalytics", "The active platform Secret Key is missing.");
        }

        private static void ValidateAds(TTGProjectConfig config, TTAdsConfig ads, BuildTarget target, List<TTGValidationResult> results)
        {
            if (!config.AppLovin.Enabled) return;
            if (string.IsNullOrWhiteSpace(config.AppLovin.SdkKey)) Error(results, "AppLovin", "MAX SDK key is missing.");
            if (ads == null) { Error(results, "Ads", "TTAdsConfig is missing while AppLovin is enabled."); return; }
            if (!ads.AdsEnabled) Warning(results, "Ads", "AppLovin is enabled while TTAdsConfig disables ads.");
            var units = target == BuildTarget.iOS ? ads.IOS : ads.Android;
            if (ads.Interstitial.Enabled && string.IsNullOrWhiteSpace(units.InterstitialId)) Error(results, "Ads", "Interstitial ad unit ID is missing for the active platform.");
            if (ads.RewardedEnabled && string.IsNullOrWhiteSpace(units.RewardedId)) Error(results, "Ads", "Rewarded ad unit ID is missing for the active platform.");
            if (ads.BannerEnabled && string.IsNullOrWhiteSpace(units.BannerId)) Error(results, "Ads", "Banner ad unit ID is missing for the active platform.");
            if (ads.MrecEnabled && string.IsNullOrWhiteSpace(units.MrecId)) Error(results, "Ads", "MREC ad unit ID is missing for the active platform.");
        }

        private static void ValidateSingular(TTGProjectConfig config, List<TTGValidationResult> results)
        {
            if (!config.Singular.Enabled) return;
            if (string.IsNullOrWhiteSpace(config.Singular.ApiKey)) Error(results, "Singular", "API key is missing.");
            if (string.IsNullOrWhiteSpace(config.Singular.ApiSecret)) Error(results, "Singular", "API secret is missing.");
            if (config.Singular.WaitForTrackingAuthorization && config.Singular.TrackingAuthorizationTimeout < 0) Error(results, "Singular", "Tracking authorization timeout cannot be negative.");
        }

        private static void ValidateIap(TTGProjectConfig config, List<TTGValidationResult> results)
        {
            if (!config.IAP.Enabled) return;
            if (!UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == "com.unity.purchasing")) Error(results, "IAP", "Unity IAP is enabled but not installed.");
            if (config.IAP.Products == null || config.IAP.Products.Count == 0) Warning(results, "IAP", "No products are configured.");
            else if (config.IAP.Products.Any(product => product == null || string.IsNullOrWhiteSpace(product.ProductId))) Error(results, "IAP", "Every IAP product requires a product ID.");
        }

        private static void Error(List<TTGValidationResult> results, string category, string message) => results.Add(new TTGValidationResult(category, message, TTGValidationSeverity.Error));
        private static void Warning(List<TTGValidationResult> results, string category, string message) => results.Add(new TTGValidationResult(category, message, TTGValidationSeverity.Warning));
    }
}
