using UnityEngine;

namespace TripleTapGames.Foundation
{
    public static class TTGConfigProvider
    {
        public const string ProjectConfigResourcePath = "TTG/TTGProjectConfig";
        public const string AdsConfigResourcePath = "TTG/TTAdsConfig";

        public static TTGProjectConfig LoadProjectConfig()
        {
            return Resources.Load<TTGProjectConfig>(ProjectConfigResourcePath);
        }

        public static TTAdsConfig LoadAdsConfig()
        {
            return Resources.Load<TTAdsConfig>(AdsConfigResourcePath);
        }
    }
}
