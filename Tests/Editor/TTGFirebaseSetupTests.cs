using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using TripleTapGames.Foundation.Editor;

namespace TripleTapGames.Foundation.Tests
{
    internal sealed class TTGFirebaseSetupTests
    {
        private string root;
        [SetUp] public void SetUp() => root = Path.Combine(Path.GetTempPath(), "ttg-firebase-test-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        [Test] public void MissingSdkIsDetected() => Assert.That(TTGFirebaseSetup.InspectAt(root, false).State, Is.EqualTo(TTGFirebaseInstallState.Missing));
        [Test] public void UpmIsRejectedEvenWithoutAssetImport() => Assert.That(TTGFirebaseSetup.InspectAt(root, true).State, Is.EqualTo(TTGFirebaseInstallState.ConflictingImport));
        [Test] public void PartialImportIsDetected()
        {
            Touch("Assets/Firebase/Plugins/Firebase.App.dll");
            Assert.That(TTGFirebaseSetup.InspectAt(root, false).State, Is.EqualTo(TTGFirebaseInstallState.Incomplete));
        }
        [Test] public void MissingVersionMetadataIsUnverified()
        {
            CreateManagedFiles();
            Assert.That(TTGFirebaseSetup.InspectAt(root, false).State, Is.EqualTo(TTGFirebaseInstallState.Unverified));
        }
        [Test] public void MixedProductVersionsAreRejected()
        {
            CreateManagedFiles();
            Touch("Assets/Firebase/Editor/FirebaseAnalytics_version-13.13.0_manifest.txt");
            Touch("Assets/Firebase/Editor/FirebaseCrashlytics_version-13.12.0_manifest.txt");
            Assert.That(TTGFirebaseSetup.InspectAt(root, false).State, Is.EqualTo(TTGFirebaseInstallState.Mismatch));
        }
        [Test] public void DuplicateVersionManifestsAreUnverified()
        {
            CreateManagedFiles();
            Touch("Assets/Firebase/Editor/FirebaseAnalytics_version-13.13.0_manifest.txt");
            Touch("Assets/Firebase/Editor/FirebaseAnalytics_version-13.12.0_manifest.txt");
            Assert.That(TTGFirebaseSetup.InspectAt(root, false).State, Is.EqualTo(TTGFirebaseInstallState.Unverified));
        }
        [Test] public void ManifestNamesAloneCannotVerifyAnInvalidDll()
        {
            CreateManagedFiles();
            Touch("Assets/Firebase/Editor/FirebaseAnalytics_version-13.13.0_manifest.txt");
            Touch("Assets/Firebase/Editor/FirebaseCrashlytics_version-13.13.0_manifest.txt");
            Assert.That(TTGFirebaseSetup.InspectAt(root, false).State, Is.EqualTo(TTGFirebaseInstallState.Unverified));
        }
        [Test] public void DisabledMissingFirebaseIsNonBlocking()
        {
            var results = Validate(false, BuildTarget.Android, TTGFirebaseInstallState.Missing, false, _ => false);
            Assert.That(results, Is.Empty);
        }
        [TestCase(TTGFirebaseInstallState.Missing)]
        [TestCase(TTGFirebaseInstallState.Incomplete)]
        [TestCase(TTGFirebaseInstallState.Unverified)]
        [TestCase(TTGFirebaseInstallState.Mismatch)]
        [TestCase(TTGFirebaseInstallState.ConflictingImport)]
        public void EnabledUnsupportedFirebaseBlocks(TTGFirebaseInstallState state)
        {
            Assert.That(Validate(true, BuildTarget.StandaloneOSX, state, true, _ => true)
                .Single().Severity, Is.EqualTo(TTGValidationSeverity.Error));
        }
        [Test] public void MissingAdapterBlocksEnabledFirebase()
            => Assert.That(Validate(true, BuildTarget.StandaloneOSX, TTGFirebaseInstallState.Verified, false, _ => true).Single().Message, Does.Contain("Refresh"));
        [Test] public void StandaloneDoesNotRequireMobileConfig()
            => Assert.That(Validate(true, BuildTarget.StandaloneOSX, TTGFirebaseInstallState.Verified, true, _ => false), Is.Empty);
        [TestCase(BuildTarget.Android, "Assets/google-services.json")]
        [TestCase(BuildTarget.iOS, "Assets/GoogleService-Info.plist")]
        public void OnlyActivePlatformConfigIsRequired(BuildTarget target, string path)
        {
            Assert.That(Validate(true, target, TTGFirebaseInstallState.Verified, true,
                p => p == path || (p != "Assets/google-services.json" && p != "Assets/GoogleService-Info.plist")), Is.Empty);
            Assert.That(Validate(true, target, TTGFirebaseInstallState.Verified, true, _ => false)
                .Any(r => r.Message.Contains(path)), Is.True);
        }
        [TestCase(BuildTarget.Android)]
        [TestCase(BuildTarget.iOS)]
        public void MissingActiveTargetNativeArtifactsBlock(BuildTarget target)
            => Assert.That(Validate(true, target, TTGFirebaseInstallState.Verified, true,
                p => p == "Assets/google-services.json" || p == "Assets/GoogleService-Info.plist").Count, Is.EqualTo(3));
        private static List<TTGValidationResult> Validate(bool enabled, BuildTarget target, TTGFirebaseInstallState state, bool adapter, Func<string, bool> exists)
        {
            var results = new List<TTGValidationResult>();
            TTGFirebaseSetup.Validate(enabled, target, new TTGFirebaseInstallation { State = state, Message = "Fixture status" }, adapter, exists, results);
            return results;
        }
        private void CreateManagedFiles()
        {
            foreach (var module in new[] { "App", "Analytics", "Crashlytics", "Platform", "TaskExtension" })
                Touch("Assets/Firebase/Plugins/Firebase." + module + ".dll");
        }
        private void Touch(string path)
        {
            var full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, "fixture, not a vendor DLL or credential");
        }
    }
}
