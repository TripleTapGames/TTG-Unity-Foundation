using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TripleTapGames.Foundation.Editor
{
    internal static class TTGVendorConfigurator
    {
        internal static void Apply(TTGProjectConfig config)
        {
            if (config == null) return;
            ApplyFacebook(config);
            ApplyAppLovin(config);
            TTGConfigurationHooks.Apply(config);
            // Singular is configured by its runtime adapter; never edit package prefabs.
            AssetDatabase.SaveAssets();
        }

        private static void ApplyFacebook(TTGProjectConfig config)
        {
            if (!config.Facebook.Enabled) return;
            var settings = AssetDatabase.LoadMainAssetAtPath("Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset");
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            SetFirstArrayString(serialized.FindProperty("appIds"), config.Facebook.AppId);
            SetFirstArrayString(serialized.FindProperty("clientTokens"), config.Facebook.ClientToken);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ApplyAppLovin(TTGProjectConfig config)
        {
            if (!config.AppLovin.Enabled) return;
            var settings = AssetDatabase.LoadMainAssetAtPath("Assets/MaxSdk/Resources/AppLovinSettings.asset");
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            var sdkKey = serialized.FindProperty("sdkKey");
            if (sdkKey != null) sdkKey.stringValue = config.AppLovin.SdkKey;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ApplySingular(TTGProjectConfig config)
        {
            if (!config.Singular.Enabled) return;
            var prefabGuid = AssetDatabase.FindAssets("SingularSDKObject t:Prefab").FirstOrDefault();
            if (string.IsNullOrEmpty(prefabGuid)) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
            var component = prefab == null ? null : prefab.GetComponents<Component>().FirstOrDefault(item => item != null && item.GetType().Name == "SingularSDK");
            if (component == null) return;
            var serialized = new SerializedObject(component);
            SetFirstString(serialized, config.Singular.ApiKey, "singularAPIKey", "apiKey", "SDKKey");
            SetFirstString(serialized, config.Singular.ApiSecret, "singularAPISecret", "apiSecret", "SDKSecret");
            SetFirstBool(serialized, false, "initializeOnAwake", "InitializeOnAwake");
            SetFirstBool(serialized, config.Singular.EnableLogging, "enableLogging", "EnableLogging");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static void SetFirstArrayString(SerializedProperty property, string value)
        {
            if (property == null || !property.isArray) return;
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).stringValue = value ?? string.Empty;
        }

        private static void SetFirstString(SerializedObject serialized, string value, params string[] names)
        {
            foreach (var name in names)
            {
                var property = serialized.FindProperty(name);
                if (property == null || property.propertyType != SerializedPropertyType.String) continue;
                property.stringValue = value ?? string.Empty;
                return;
            }
        }

        private static void SetFirstBool(SerializedObject serialized, bool value, params string[] names)
        {
            foreach (var name in names)
            {
                var property = serialized.FindProperty(name);
                if (property == null || property.propertyType != SerializedPropertyType.Boolean) continue;
                property.boolValue = value;
                return;
            }
        }
    }
}
