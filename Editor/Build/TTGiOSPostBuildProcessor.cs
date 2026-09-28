#if UNITY_IOS
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TripleTapGames.Foundation.Editor
{
    public interface ITTGiOSBuildContributor
    {
        int Order { get; }
        void Apply(string buildPath, PBXProject project, PlistDocument plist);
    }

    internal static class TTGiOSPostBuildProcessor
    {
        private static readonly List<ITTGiOSBuildContributor> Contributors = new List<ITTGiOSBuildContributor>();
        public static void Register(ITTGiOSBuildContributor contributor) { if (contributor != null && !Contributors.Contains(contributor)) Contributors.Add(contributor); }

        [PostProcessBuild(1000)]
        private static void Process(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var plistPath = System.IO.Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            Contributors.Sort((left, right) => left.Order.CompareTo(right.Order));
            foreach (var contributor in Contributors) contributor.Apply(path, project, plist);
            project.WriteToFile(projectPath);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
