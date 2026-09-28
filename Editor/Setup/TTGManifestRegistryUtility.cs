using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TripleTapGames.Foundation.Editor
{
    internal static class TTGManifestRegistryUtility
    {
        private const string AppLovinUrl = "https://unity.packages.applovin.com/";
        private const string OpenUpmUrl = "https://package.openupm.com";

        internal static void EnsureRequiredRegistries()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;
            var path = Path.Combine(projectRoot, "Packages", "manifest.json");
            var original = File.ReadAllText(path);
            var updated = EnsureRegistry(original, "AppLovin MAX Unity", AppLovinUrl,
                "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp");
            updated = EnsureRegistry(updated, "package.openupm.com", OpenUpmUrl,
                "com.gameanalytics", "com.google.external-dependency-manager");
            if (updated == original) return;
            File.WriteAllText(path, updated);
            AssetDatabase.Refresh();
        }

        internal static string EnsureRegistry(string json, string name, string url, params string[] scopes)
        {
            var registryIndex = json.IndexOf("\"url\"", StringComparison.Ordinal);
            while (registryIndex >= 0)
            {
                var objectStart = json.LastIndexOf('{', registryIndex);
                var objectEnd = FindMatching(json, objectStart, '{', '}');
                if (objectStart >= 0 && objectEnd > registryIndex)
                {
                    var block = json.Substring(objectStart, objectEnd - objectStart + 1);
                    if (block.IndexOf("\"" + url + "\"", StringComparison.Ordinal) >= 0)
                    {
                        foreach (var scope in scopes) block = EnsureScope(block, scope);
                        return json.Substring(0, objectStart) + block + json.Substring(objectEnd + 1);
                    }
                }
                registryIndex = json.IndexOf("\"url\"", registryIndex + 5, StringComparison.Ordinal);
            }

            var registry = "{\n      \"name\": \"" + name + "\",\n      \"url\": \"" + url + "\",\n      \"scopes\": [\"" + string.Join("\", \"", scopes) + "\"]\n    }";
            var keyIndex = json.IndexOf("\"scopedRegistries\"", StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                var arrayStart = json.IndexOf('[', keyIndex);
                var arrayEnd = FindMatching(json, arrayStart, '[', ']');
                var hasEntries = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1).Trim().Length > 0;
                return json.Insert(arrayEnd, (hasEntries ? ",\n    " : "\n    ") + registry + "\n  ");
            }

            var rootEnd = json.LastIndexOf('}');
            var prefix = json.Substring(0, rootEnd).TrimEnd();
            return prefix + ",\n  \"scopedRegistries\": [\n    " + registry + "\n  ]\n}" + json.Substring(rootEnd + 1);
        }

        private static string EnsureScope(string registryBlock, string scope)
        {
            if (registryBlock.IndexOf("\"" + scope + "\"", StringComparison.Ordinal) >= 0) return registryBlock;
            var scopesIndex = registryBlock.IndexOf("\"scopes\"", StringComparison.Ordinal);
            var arrayStart = registryBlock.IndexOf('[', scopesIndex);
            var arrayEnd = FindMatching(registryBlock, arrayStart, '[', ']');
            var hasEntries = registryBlock.Substring(arrayStart + 1, arrayEnd - arrayStart - 1).Trim().Length > 0;
            return registryBlock.Insert(arrayEnd, (hasEntries ? ", " : string.Empty) + "\"" + scope + "\"");
        }

        private static int FindMatching(string text, int start, char open, char close)
        {
            if (start < 0) return -1;
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var i = start; i < text.Length; i++)
            {
                var character = text[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                    continue;
                }
                if (character == '"') { inString = true; continue; }
                if (character == open) depth++;
                else if (character == close && --depth == 0) return i;
            }
            return -1;
        }
    }
}
