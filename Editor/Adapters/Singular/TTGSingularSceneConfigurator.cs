using System;
using System.Linq;
using Singular;
using TripleTapGames.Foundation.Adapters.Singular;
using UnityEditor;
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
}
