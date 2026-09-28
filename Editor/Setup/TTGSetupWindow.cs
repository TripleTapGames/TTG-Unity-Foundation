using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TripleTapGames.Foundation.Editor
{
    public sealed class TTGSetupWindow : EditorWindow
    {
        private Vector2 scroll;
        private IReadOnlyList<TTGValidationResult> validationResults;

        [MenuItem("Tools/Triple Tap Games/Project Setup")]
        public static void Open() => GetWindow<TTGSetupWindow>("TTG Project Setup");

        private void OnEnable() => TTGPackageInstaller.Changed += Repaint;
        private void OnDisable() => TTGPackageInstaller.Changed -= Repaint;

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("TTG Foundation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Project values are stored in Assets/TTGGenerated and are ignored by the generated folder policy. Never put backend secrets in a client build.", MessageType.Info);
            DrawDependencies();
            DrawFirebase();
            DrawConfiguration();
            DrawValidation();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDependencies()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
            foreach (var dependency in TTGDependencyCatalog.All)
            {
                EditorGUILayout.BeginHorizontal();
                var installed = dependency.IsInstalled();
                EditorGUILayout.LabelField((installed ? "✓ " : "○ ") + dependency.Name + " " + dependency.Version);
                if (!installed && dependency.InstallKind == TTGDependencyInstallKind.GuidedImport && GUILayout.Button("Instructions", GUILayout.Width(90)))
                    TTGPackageInstaller.OpenGuidance(dependency);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.BeginDisabledGroup(TTGPackageInstaller.IsBusy);
            if (GUILayout.Button(TTGPackageInstaller.IsBusy ? "Installing…" : "Install Missing Packages")) TTGPackageInstaller.InstallMissing();
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawConfiguration()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            if (GUILayout.Button("Create / Update Local Loading Scene")) TTGLoadingSceneSetup.CreateOrUpdate();
            if (GUILayout.Button("Create / Update Base Game Flow")) TTGGameFlowSetup.CreateOrUpdate();
            if (GUILayout.Button("Create / Locate TTG Project Config")) TTGConfigAssetUtility.Select(TTGConfigAssetUtility.GetOrCreateProjectConfig());
            if (GUILayout.Button("Create / Locate TTG Ads Config")) TTGConfigAssetUtility.Select(TTGConfigAssetUtility.GetOrCreateAdsConfig());
            if (GUILayout.Button("Import Values From Environment")) TTGConfigAssetUtility.ImportEnvironment(TTGConfigAssetUtility.GetOrCreateProjectConfig());
            if (GUILayout.Button("Apply Configuration"))
            {
                TTGVendorConfigurator.Apply(TTGConfigAssetUtility.GetOrCreateProjectConfig());
                TTGDependencyDefines.ApplyForActiveTarget();
                AssetDatabase.SaveAssets();
                Debug.Log("[TTG:Setup] Configuration and dependency defines applied for the active target.");
            }
            if (GUILayout.Button("Open Documentation")) Application.OpenURL("https://github.com/TripleTapGames");
        }

        private static void DrawFirebase()
        {
            var status = TTGFirebaseSetup.Inspect();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Firebase — local guided installation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Required: " + TTGFirebaseSetup.RequiredVersion + " | Detected: " + status.DetectedVersion);
            EditorGUILayout.HelpBox(status.State + ": " + status.Message, status.IsSupported ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.HelpBox("Download the pinned Analytics and Crashlytics .unitypackage files. Import both through Assets > Import Package > Custom Package. Deselect Assets/ExternalDependencyManager: this template already uses its UPM package. Do not add Firebase UPM packages. Then refresh the local adapter. Add your game's google-services.json (Android) / GoogleService-Info.plist (iOS) under Assets; these stay out of Git.", MessageType.Info);
            if (GUILayout.Button("Open Official Firebase Downloads")) Application.OpenURL(TTGFirebaseSetup.DownloadUrl);
            if (GUILayout.Button("Open Firebase Import Instructions")) Application.OpenURL(TTGFirebaseSetup.GuidanceUrl);
            EditorGUILayout.LabelField("Local adapter: " + (TTGFirebaseSetup.AdapterIsCurrent ? "Generated (wait for compilation)" : "Missing or outdated"));
            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling || EditorApplication.isUpdating);
            if (GUILayout.Button("Refresh Firebase Adapter")) TTGFirebaseSetup.RefreshAdapter();
            if (GUILayout.Button("Disable Firebase Adapter Before SDK Removal")) TTGFirebaseSetup.DisableAdapter();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawValidation()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate Project")) validationResults = TTGSetupValidator.Validate(EditorUserBuildSettings.activeBuildTarget);
            if (validationResults == null) return;
            foreach (var result in validationResults)
            {
                var type = result.Severity == TTGValidationSeverity.Error ? MessageType.Error
                    : result.Severity == TTGValidationSeverity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox("[" + result.Category + "] " + result.Message, type);
            }
        }
    }
}
