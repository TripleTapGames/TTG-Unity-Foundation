using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace TripleTapGames.Foundation.Editor
{
    internal enum TTGDependencyInstallKind { Package, GuidedImport }

    internal sealed class TTGDependency
    {
        public string Name;
        public string Version;
        public string PackageId;
        public string InstallValue;
        public TTGDependencyInstallKind InstallKind;
        public Func<bool> AssetDetector;
        public string DocumentationUrl;

        public bool IsInstalled()
        {
            if (AssetDetector != null && AssetDetector()) return true;
            return !string.IsNullOrEmpty(PackageId) && UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == PackageId);
        }
    }

    internal static class TTGDependencyCatalog
    {
        internal static readonly IReadOnlyList<TTGDependency> All = new[]
        {
            new TTGDependency { Name = "UniTask", Version = "2.5.11", PackageId = "com.cysharp.unitask", InstallValue = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11", InstallKind = TTGDependencyInstallKind.Package },
            new TTGDependency { Name = "AppLovin MAX", Version = "8.6.6", PackageId = "com.applovin.mediation.ads", InstallValue = "com.applovin.mediation.ads@8.6.6", InstallKind = TTGDependencyInstallKind.Package },
            new TTGDependency { Name = "GameAnalytics", Version = "8.2.0", PackageId = "com.gameanalytics.sdk", InstallValue = "com.gameanalytics.sdk@8.2.0", InstallKind = TTGDependencyInstallKind.Package, AssetDetector = () => Directory.Exists("Assets/GameAnalytics"), DocumentationUrl = "https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/" },
            new TTGDependency { Name = "Singular", Version = "5.10.1", PackageId = "singular-unity-package", InstallValue = "https://github.com/singular-labs/Singular-Unity-SDK.git#5.10.1", InstallKind = TTGDependencyInstallKind.Package, DocumentationUrl = "https://support.singular.net/hc/en-us/articles/360037635452-Unity-SDK-Basic-Integration" },
            new TTGDependency { Name = "Unity IAP", Version = "4.14.0", PackageId = "com.unity.purchasing", InstallValue = "com.unity.purchasing@4.14.0", InstallKind = TTGDependencyInstallKind.Package },
            new TTGDependency { Name = "Firebase Analytics + Crashlytics", Version = TTGFirebaseSetup.RequiredVersion, InstallKind = TTGDependencyInstallKind.GuidedImport, AssetDetector = () => TTGFirebaseSetup.Inspect().IsSupported, DocumentationUrl = TTGFirebaseSetup.GuidanceUrl },
            new TTGDependency { Name = "Facebook SDK", Version = "18.1.0 (17.x supported)", InstallKind = TTGDependencyInstallKind.GuidedImport, AssetDetector = () => File.Exists("Assets/FacebookSDK/Plugins/Facebook.Unity.dll"), DocumentationUrl = "https://github.com/facebook/facebook-sdk-for-unity/releases" },
            new TTGDependency { Name = "DOTween", Version = "1.3.030", InstallKind = TTGDependencyInstallKind.GuidedImport, AssetDetector = () => Directory.Exists("Assets/Plugins/Demigiant/DOTween"), DocumentationUrl = "https://dotween.demigiant.com/download" }
        };
    }

    internal static class TTGPackageInstaller
    {
        private static readonly Queue<TTGDependency> Queue = new Queue<TTGDependency>();
        private static AddRequest request;

        internal static bool IsBusy => request != null || Queue.Count > 0;
        internal static event Action Changed;

        internal static void InstallMissing()
        {
            if (IsBusy) return;
            TTGManifestRegistryUtility.EnsureRequiredRegistries();
            foreach (var dependency in TTGDependencyCatalog.All)
            {
                if (dependency.InstallKind == TTGDependencyInstallKind.Package && !dependency.IsInstalled()) Queue.Enqueue(dependency);
            }
            EditorApplication.update += Update;
            Update();
        }

        private static void Update()
        {
            if (request != null && !request.IsCompleted) return;
            if (request != null)
            {
                if (request.Status == StatusCode.Failure)
                    Debug.LogError("[TTG:Setup] Package installation failed: " + request.Error.message);
                request = null;
                Changed?.Invoke();
            }
            if (Queue.Count == 0)
            {
                EditorApplication.update -= Update;
                Changed?.Invoke();
                return;
            }
            var dependency = Queue.Dequeue();
            request = Client.Add(dependency.InstallValue);
        }

        internal static void OpenGuidance(TTGDependency dependency)
        {
            if (!string.IsNullOrEmpty(dependency.DocumentationUrl)) Application.OpenURL(dependency.DocumentationUrl);
        }
    }
}
