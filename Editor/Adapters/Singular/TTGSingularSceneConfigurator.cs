using System;
using System.Linq;
using Singular;
using TripleTapGames.Foundation.Adapters.Singular;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TripleTapGames.Foundation.Editor
{
    [InitializeOnLoad]
    public static class TTGSingularSceneConfigurator
    {
        static TTGSingularSceneConfigurator() => TTGLoadingSceneSetup.Configuring += Apply;

        public static void Apply(TTGProjectConfig config, Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var all = roots.SelectMany(r => r.GetComponentsInChildren<SingularSDK>(true)).ToArray();
            if (all.Length > 1) throw new InvalidOperationException("Loading contains duplicate Singular components. Keep one SingularSDKObject.");
            var sdk = all.SingleOrDefault();
            if (sdk == null)
            {
                var named = roots.Where(r => r.name == "SingularSDKObject").ToArray();
                if (named.Length > 1) throw new InvalidOperationException("Loading contains duplicate SingularSDKObject roots.");
                var go = named.SingleOrDefault();
                if (go == null) { go = new GameObject("SingularSDKObject"); SceneManager.MoveGameObjectToScene(go, scene); }
                go.SetActive(false);
                sdk = Undo.AddComponent<SingularSDK>(go);
            }
            if (sdk.transform.parent != null) throw new InvalidOperationException("Keep SingularSDKObject at the root of the Loading scene.");
            Undo.RecordObject(sdk, "Apply TTG Singular Configuration");
            TTGSingularConfiguration.Apply(config.Singular, sdk);
            // Activate only in TTG's runtime initialization, after consent has resolved.
            sdk.gameObject.SetActive(false);
            EditorUtility.SetDirty(sdk);
            if (PrefabUtility.IsPartOfPrefabInstance(sdk)) PrefabUtility.RecordPrefabInstancePropertyModifications(sdk);
        }
    }
    /// <summary>Report stale authored startup before it can bypass TTG consent/configuration.</summary>
    internal sealed class TTGSingularStartupValidator : IProcessSceneWithReport
    {
        public int callbackOrder => -1000;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            var config = TTGConfigProvider.LoadProjectConfig();
            if (config == null || !config.Singular.Enabled) return;
            foreach (var sdk in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingularSDK>(true)))
                if (sdk.gameObject.activeInHierarchy && sdk.InitializeOnAwake)
                    throw new BuildFailedException("[TTG:Singular] " + scene.path
                        + " contains automatic Singular startup. Regenerate Loading through TTG Setup, or disable InitializeOnAwake and let TTG own initialization.");
        }
    }

}
