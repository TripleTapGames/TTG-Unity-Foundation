using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TripleTapGames.Foundation.Editor
{
    internal enum TTGFirebaseInstallState { Missing, Incomplete, Verified, Unverified, Mismatch, ConflictingImport }

    internal sealed class TTGFirebaseInstallation
    {
        internal TTGFirebaseInstallState State;
        internal string DetectedVersion;
        internal string Message;
        internal bool IsSupported => State == TTGFirebaseInstallState.Verified;
    }

    // No Firebase references: this tooling must compile before the SDK is installed.
    public static class TTGFirebaseSetup
    {
        public const string RequiredVersion = "13.13.0";
        public const string GuidanceUrl = "https://firebase.google.com/docs/unity/setup-alternative";
        public const string DownloadUrl = "https://developers.google.com/unity/archive";
        internal const string AdapterRoot = "Assets/TTGGenerated/FirebaseAdapter";
        private const string FirebaseAdapterRelativePath = "Runtime/Adapters/Firebase/FirebaseService.cs";
        private const string AssemblyJson = "{\"name\":\"TTG.Foundation.Firebase.Local\",\"references\":[\"TTG.Foundation.Runtime\",\"UniTask\"],\"autoReferenced\":true}";
        private static readonly string[] Modules = { "App", "Analytics", "Crashlytics", "Platform", "TaskExtension" };

        internal static TTGFirebaseInstallation Inspect()
        {
            return InspectAt(".", UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .Any(p => p.name.StartsWith("com.google.firebase.", StringComparison.Ordinal)));
        }

        internal static TTGFirebaseInstallation InspectAt(string root, bool firebaseUpm)
        {
            if (firebaseUpm) return Result(TTGFirebaseInstallState.ConflictingImport, "UPM detected",
                "Firebase UPM packages are unsupported in this asset-import template. Do not mix import methods.");
            var sdk = Path.Combine(root, "Assets/Firebase");
            if (!Directory.Exists(sdk)) return Result(TTGFirebaseInstallState.Missing, "none", "Import Firebase Analytics and Crashlytics 13.13.0, then Refresh Firebase Adapter.");
            if (Modules.Any(m => !File.Exists(Path.Combine(sdk, "Plugins/Firebase." + m + ".dll"))))
                return Result(TTGFirebaseInstallState.Incomplete, "incomplete", "Firebase core, Analytics and Crashlytics managed dependencies are required together. Reimport both pinned packages.");
            var editor = Path.Combine(sdk, "Editor");
            var versions = new List<string>();
            foreach (var product in new[] { "Analytics", "Crashlytics" })
            {
                var manifests = Directory.Exists(editor) ? Directory.GetFiles(editor, "Firebase" + product + "_version-*_manifest.txt") : Array.Empty<string>();
                if (manifests.Length != 1) return Result(TTGFirebaseInstallState.Unverified, "unknown or multiple manifests", "Keep exactly one vendor version manifest for each imported Firebase product; reinstall from trusted pinned artifacts.");
                var match = Regex.Match(Path.GetFileName(manifests[0]), @"_version-(\d+\.\d+\.\d+)_manifest\.txt$");
                if (!match.Success) return Result(TTGFirebaseInstallState.Unverified, "unknown", "Firebase version metadata could not be read.");
                versions.Add(match.Groups[1].Value);
            }
            if (versions.Any(v => v != RequiredVersion))
                return Result(TTGFirebaseInstallState.Mismatch, string.Join(", ", versions.Distinct()), "Both Firebase products must be version " + RequiredVersion + ".");
            // Inspect metadata without loading or invoking SDK code.
            try
            {
                foreach (var module in Modules.Take(3))
                {
                    var assembly = AssemblyName.GetAssemblyName(Path.Combine(sdk, "Plugins/Firebase." + module + ".dll"));
                    // Firebase uses assembly version 1.0.0.0, not the SDK release version.
                    if (assembly.Name != "Firebase." + module)
                        return Result(TTGFirebaseInstallState.Unverified, "unexpected DLL identity", "Firebase DLL identity does not match its product.");
                }
            }
            catch (Exception)
            {
                return Result(TTGFirebaseInstallState.Unverified, "unreadable DLL metadata", "Reimport Firebase from trusted pinned artifacts; no adapter was enabled.");
            }
            var native = Path.Combine(sdk, "Plugins/x86_64");
            var pinnedNative = "FirebaseCppApp-" + RequiredVersion.Replace('.', '_');
            if (!Directory.Exists(native) || !Directory.GetFiles(native, pinnedNative + ".*").Any(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
                return Result(TTGFirebaseInstallState.Incomplete, RequiredVersion + " manifests", "Pinned Firebase desktop core library is missing. Reimport complete vendor artifacts.");
            if (Directory.GetFiles(native, "FirebaseCppApp-*").Any(p => !Path.GetFileName(p).StartsWith(pinnedNative + ".", StringComparison.Ordinal)))
                return Result(TTGFirebaseInstallState.Mismatch, "mixed native versions", "Remove the old Firebase installation using vendor guidance before importing the pin.");
            var extension = Application.platform == RuntimePlatform.OSXEditor ? ".bundle"
                : Application.platform == RuntimePlatform.WindowsEditor ? ".dll" : ".so";
            if (!File.Exists(Path.Combine(native, pinnedNative + extension))
                || !File.Exists(Path.Combine(native, "FirebaseCppAnalytics" + extension)))
                return Result(TTGFirebaseInstallState.Incomplete, RequiredVersion + " manifests", "Native Firebase App/Analytics libraries for this Editor platform are missing. Reimport the pinned artifacts.");
            return Result(TTGFirebaseInstallState.Verified, RequiredVersion, "Vendor release manifests, DLL identities and desktop core filename verified. This is not a native build or checksum verification.");
        }

        private static TTGFirebaseInstallation Result(TTGFirebaseInstallState state, string version, string message)
            => new TTGFirebaseInstallation { State = state, DetectedVersion = version, Message = message };

        private static string Source
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TTGFirebaseSetup).Assembly);
                if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
                    throw new InvalidOperationException("Unity Package Manager could not resolve the TTG Foundation installation path.");
                return Path.Combine(package.resolvedPath, FirebaseAdapterRelativePath);
            }
        }

        internal static bool AdapterIsCurrent => File.Exists(Source)
            && File.Exists(AdapterRoot + "/FirebaseService.cs") && File.Exists(AdapterRoot + "/TTG.Foundation.Firebase.Local.asmdef")
            && File.ReadAllText(Source) == File.ReadAllText(AdapterRoot + "/FirebaseService.cs")
            && File.ReadAllText(AdapterRoot + "/TTG.Foundation.Firebase.Local.asmdef") == AssemblyJson;

        public static void RefreshAdapter()
        {
            RemoveLegacyDefines();
            var status = Inspect();
            if (!status.IsSupported)
            {
                DisableAdapter();
                Debug.LogWarning("[TTG:Firebase] " + status.Message);
                return;
            }
            if (AdapterIsCurrent) return;
            Directory.CreateDirectory(AdapterRoot);
            File.Copy(Source, AdapterRoot + "/FirebaseService.cs", true);
            File.WriteAllText(AdapterRoot + "/TTG.Foundation.Firebase.Local.asmdef", AssemblyJson);
            AssetDatabase.Refresh();
            Debug.Log("[TTG:Firebase] Local adapter generated. Wait for compilation, then validate the active target.");
        }

        public static void DisableAdapter()
        {
            RemoveLegacyDefines();
            // Delete only our reproducible generated adapter, never SDKs or configuration.
            if (Directory.Exists(AdapterRoot) && !AssetDatabase.DeleteAsset(AdapterRoot))
                throw new IOException("Could not remove the generated Firebase adapter. Do not remove the SDK until the adapter has been disabled.");
            AssetDatabase.Refresh();
            Debug.Log("[TTG:Firebase] Local adapter disabled. After compilation finishes, you may uninstall Firebase using vendor guidance. SDK files have not been removed.");
        }

        private static void RemoveLegacyDefines()
        {
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS })
            {
                var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                var cleaned = string.Join(";", current.Split(';').Where(s => s != "TTG_FIREBASE"));
                if (current != cleaned) PlayerSettings.SetScriptingDefineSymbolsForGroup(group, cleaned);
            }
        }

        internal static void Validate(bool enabled, BuildTarget target, TTGFirebaseInstallation status,
            bool adapterCurrent, Func<string, bool> exists, List<TTGValidationResult> results)
        {
            if (!enabled) return;
            if (!status.IsSupported) results.Add(new TTGValidationResult("Firebase", status.Message, TTGValidationSeverity.Error));
            else if (!adapterCurrent) results.Add(new TTGValidationResult("Firebase", "Click Refresh Firebase Adapter and wait for compilation before building.", TTGValidationSeverity.Error));
            if (target == BuildTarget.Android && !exists("Assets/google-services.json"))
                results.Add(new TTGValidationResult("Firebase", "Assets/google-services.json is missing for Android.", TTGValidationSeverity.Error));
            if (target == BuildTarget.iOS && !exists("Assets/GoogleService-Info.plist"))
                results.Add(new TTGValidationResult("Firebase", "Assets/GoogleService-Info.plist is missing for iOS.", TTGValidationSeverity.Error));
            if (!status.IsSupported) return;
            foreach (var product in new[] { "App", "Analytics", "Crashlytics" })
            {
                if (target == BuildTarget.iOS && !exists("Assets/Plugins/iOS/Firebase/libFirebaseCpp" + product + ".a"))
                    results.Add(new TTGValidationResult("Firebase", "Missing iOS Firebase " + product + " native library. Reimport complete pinned artifacts.", TTGValidationSeverity.Error));
                if (target == BuildTarget.Android)
                {
                    var id = "firebase-" + product.ToLowerInvariant() + "-unity";
                    var relative = "com/google/firebase/" + id + "/" + RequiredVersion + "/" + id + "-" + RequiredVersion;
                    if (!exists("Assets/Firebase/m2repository/" + relative + ".srcaar")
                        && !exists("Assets/GeneratedLocalRepo/Firebase/m2repository/" + relative + ".aar"))
                        results.Add(new TTGValidationResult("Firebase", "Missing Android Firebase " + product + " artifact. Reimport complete pinned artifacts and resolve dependencies.", TTGValidationSeverity.Error));
                }
            }
        }
    }
}
