using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGSamplePackagingTests
    {
        private static string Root => PackageInfo.FindForAssembly(typeof(TTGSamplePackagingTests).Assembly).resolvedPath + Path.DirectorySeparatorChar;
        [Serializable] private sealed class Manifest { public Entry[] samples; }
        [Serializable] private sealed class Entry { public string path; }

        [Test]
        public void EveryDeclaredSampleIsIncludedInThePackage()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "package.json"));
            Assert.That(manifest.samples, Is.Not.Empty);
            foreach (var sample in manifest.samples)
            {
                var path = Root + sample.path;
                Assert.That(Directory.Exists(path), Is.True, "Missing declared sample: " + sample.path);
                Assert.That(Directory.GetFiles(path, "*", SearchOption.AllDirectories), Is.Not.Empty);
            }
            Assert.That(File.Exists(Root + "Samples~/BootstrapExample/TTGBootstrapExample.cs"), Is.True);
        }

        [TestCase("GettingStarted.md")]
        [TestCase("UserGuide.md")]
        [TestCase("FirebaseInstallation.md")]
        [TestCase("Troubleshooting.md")]
        [TestCase("Upgrading.md")]
        [TestCase("GameFlow.md")]
        [TestCase("AnalyticsEvents.md")]
        public void UserDocumentationIsIncluded(string file)
            => Assert.That(File.Exists(Root + "Documentation~/" + file), Is.True, "Missing package documentation: " + file);
    }
}
