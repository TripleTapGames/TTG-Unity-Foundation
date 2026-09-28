using System;
using System.Collections.Generic;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    public enum TTGEnvironment
    {
        Development,
        Staging,
        Production
    }

    [Serializable]
    public class TTGServiceConfig
    {
        public bool Enabled;
        public bool Required;
    }

    [Serializable]
    public sealed class TTGFacebookConfig : TTGServiceConfig
    {
        public string AppId;
        public string ClientToken;
    }

    [Serializable]
    public sealed class TTGFirebaseConfig : TTGServiceConfig
    {
        public bool AnalyticsEnabled = true;
        public bool CrashlyticsEnabled = true;
    }

    [Serializable]
    public sealed class TTGGameAnalyticsConfig : TTGServiceConfig
    {
        public string AndroidGameKey;
        public string AndroidSecretKey;
        public string IosGameKey;
        public string IosSecretKey;
    }

    [Serializable]
    public sealed class TTGAppLovinConfig : TTGServiceConfig
    {
        public string SdkKey;
        public string PrivacyPolicyUrl = "https://tripletapgames.com/privacy-policy/";
    }

    [Serializable]
    public sealed class TTGSingularConfig : TTGServiceConfig
    {
        public string ApiKey;
        public string ApiSecret;
        public bool InitializeOnAwake = true;
        public bool EnableLogging = true;
        [Range(0, 3)] public int LogLevel = 3;
        public bool SkanEnabled = true;
        public bool WaitForTrackingAuthorization = true;
        public int TrackingAuthorizationTimeout = 300;
        public bool OdmEnabled = true;
        public int OdmTimeout = -1;
    }

    public enum TTGIAPProductType
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    [Serializable]
    public sealed class TTGIAPProduct
    {
        public string ProductId;
        public TTGIAPProductType ProductType;
    }

    [Serializable]
    public sealed class TTGIAPConfig : TTGServiceConfig
    {
        public List<TTGIAPProduct> Products = new List<TTGIAPProduct>();
    }

    [CreateAssetMenu(fileName = "TTGProjectConfig", menuName = "Triple Tap Games/Foundation/Project Config")]
    public sealed class TTGProjectConfig : ScriptableObject
    {
        public TTGEnvironment Environment = TTGEnvironment.Development;
        public bool EnableDebugLogging = true;
        public TTGFacebookConfig Facebook = new TTGFacebookConfig();
        public TTGFirebaseConfig Firebase = new TTGFirebaseConfig();
        public TTGGameAnalyticsConfig GameAnalytics = new TTGGameAnalyticsConfig();
        public TTGAppLovinConfig AppLovin = new TTGAppLovinConfig();
        public TTGSingularConfig Singular = new TTGSingularConfig();
        public TTGIAPConfig IAP = new TTGIAPConfig();
    }
}
