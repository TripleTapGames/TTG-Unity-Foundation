#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace TripleTapGames.Foundation.Editor
{
    internal sealed class TTGAndroidBuildProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var candidates = new[]
            {
                Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "launcher", "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "unityLibrary", "src", "main", "AndroidManifest.xml")
            };
            foreach (var manifest in candidates)
            {
                if (File.Exists(manifest)) PatchManifest(manifest);
            }
        }

        internal static void PatchManifest(string manifestPath)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifestPath);
            var application = document.SelectSingleNode("/manifest/application") as XmlElement;
            if (application == null) return;
            application.SetAttribute("debuggable", AndroidNamespace, "false");
            application.SetAttribute("allowBackup", AndroidNamespace, "false");
            document.Save(manifestPath);
        }
    }
}
#endif
