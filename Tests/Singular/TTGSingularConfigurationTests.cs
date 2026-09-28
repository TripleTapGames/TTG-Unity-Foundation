using System;
using System.Linq;
using NUnit.Framework;
using Singular;
using TripleTapGames.Foundation.Adapters.Singular;
using TripleTapGames.Foundation.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGSingularConfigurationTests
    {
        private Scene scene;
        private TTGProjectConfig config;
        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            config = ScriptableObject.CreateInstance<TTGProjectConfig>();
            config.Singular.ApiKey = "fixture-key";
            config.Singular.ApiSecret = "fixture-secret";
        }
        [TearDown] public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(config);
        }
        private SingularSDK AddSdk()
        {
            var go = new GameObject("SingularSDKObject");
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<SingularSDK>();
        }
        [Test] public void MapsKeysAndSupportedFlagsWithSafeStartup()
        {
            var sdk = AddSdk();
            sdk.facebookAppId = "untouched-fixture";
            config.Singular.InitializeOnAwake = true; config.Singular.EnableLogging = true;
            config.Singular.SkanEnabled = false; config.Singular.TrackingAuthorizationTimeout = 27;
            config.Singular.OdmEnabled = true; config.Singular.OdmTimeout = 15;
            TTGSingularConfiguration.Apply(config.Singular, sdk);
            Assert.That(sdk.SingularAPIKey, Is.EqualTo("fixture-key"));
            Assert.That(sdk.SingularAPISecret, Is.EqualTo("fixture-secret"));
            Assert.That(sdk.InitializeOnAwake, Is.False);
            Assert.That(sdk.enableLogging, Is.False);
            Assert.That(sdk.SKANEnabled, Is.False);
            Assert.That(sdk.waitForTrackingAuthorizationWithTimeoutInterval, Is.EqualTo(27));
            Assert.That(sdk.enableODMWithTimeoutInterval, Is.EqualTo(15));
            Assert.That(sdk.facebookAppId, Is.EqualTo("untouched-fixture"));
        }
        [Test] public void DisabledWaitAndOdmUseVendorDisabledValues()
        {
            var sdk = AddSdk();
            config.Singular.WaitForTrackingAuthorization = false; config.Singular.OdmEnabled = false;
            config.Singular.ApiKey = null; config.Singular.ApiSecret = null;
            TTGSingularConfiguration.Apply(config.Singular, sdk);
            Assert.That(sdk.SingularAPIKey, Is.Empty);
            Assert.That(sdk.SingularAPISecret, Is.Empty);
            Assert.That(sdk.waitForTrackingAuthorizationWithTimeoutInterval, Is.Zero);
            Assert.That(sdk.enableODMWithTimeoutInterval, Is.EqualTo(-1));
        }
        [Test] public void RepeatedApplyReusesInactiveObjectAndUpdatesKeys()
        {
            var sdk = AddSdk();
            TTGSingularSceneConfigurator.Apply(config, scene);
            config.Singular.ApiKey = "fixture-new-key";
            TTGSingularSceneConfigurator.Apply(config, scene);
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SingularSDK>(true)).Count(), Is.EqualTo(1));
            Assert.That(sdk.SingularAPIKey, Is.EqualTo("fixture-new-key"));
            Assert.That(sdk.gameObject.activeSelf, Is.False);
        }
        [Test] public void CreatesObjectWithoutChangingOtherSceneRoots()
        {
            var other = new GameObject("User UI"); SceneManager.MoveGameObjectToScene(other, scene);
            TTGSingularSceneConfigurator.Apply(config, scene);
            Assert.That(scene.GetRootGameObjects().Contains(other), Is.True);
            Assert.That(scene.GetRootGameObjects().Single(r => r.name == "SingularSDKObject").activeSelf, Is.False);
        }
        [Test] public void DuplicateComponentsFailWithoutChangingKeys()
        {
            var one = AddSdk(); AddSdk(); one.SingularAPIKey = "fixture-original";
            Assert.Throws<InvalidOperationException>(() => TTGSingularSceneConfigurator.Apply(config, scene));
            Assert.That(one.SingularAPIKey, Is.EqualTo("fixture-original"));
        }
    }
}
