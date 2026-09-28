using System;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    public enum TTGBannerPosition
    {
        Top,
        Bottom
    }

    [Serializable]
    public sealed class TTGPlatformAdUnits
    {
        public string BannerId;
        public string InterstitialId;
        public string RewardedId;
        public string MrecId;
        public string AppOpenId;
    }

    [Serializable]
    public sealed class TTGInterstitialRules
    {
        public bool Enabled = true;
        [Min(0)] public float MinimumSessionTime;
        [Min(1)] public int LevelInterval = 1;
        [Min(0)] public float CooldownSeconds;
    }

    [CreateAssetMenu(fileName = "TTAdsConfig", menuName = "Triple Tap Games/Foundation/Ads Config")]
    public sealed class TTAdsConfig : ScriptableObject
    {
        public bool AdsEnabled;
        public bool TestMode;
        public bool DebugLogging;
        public TTGPlatformAdUnits Android = new TTGPlatformAdUnits();
        public TTGPlatformAdUnits IOS = new TTGPlatformAdUnits();
        public TTGInterstitialRules Interstitial = new TTGInterstitialRules();
        public bool RewardedEnabled = true;
        public bool BannerEnabled;
        public TTGBannerPosition DefaultBannerPosition = TTGBannerPosition.Bottom;
        public bool MrecEnabled;

        public TTGPlatformAdUnits GetPlatformUnits(RuntimePlatform platform)
        {
            return platform == RuntimePlatform.IPhonePlayer ? IOS : Android;
        }

        public TTGPlatformAdUnits GetCurrentPlatformUnits()
        {
            return GetPlatformUnits(Application.platform);
        }
    }
}
