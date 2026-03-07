using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace TraceForge.Tests.Editor
{
    [TestFixture]
    public class PackageMetaValidationTests
    {
        private string _packageRoot;

        [SetUp]
        public void SetUp()
        {
            var assembly = typeof(TF).Assembly;
            var packageInfo = PackageInfo.FindForAssembly(assembly);
            Assert.IsNotNull(packageInfo, "PackageInfo not found for TraceForge.Runtime assembly");
            _packageRoot = packageInfo.resolvedPath;
        }

        [Test]
        public void AllAssets_HaveMetaFiles()
        {
            var failures = new List<string>();

            foreach (var file in Directory.GetFiles(_packageRoot, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta") || IsExcluded(file)) continue;
                if (!File.Exists(file + ".meta"))
                    failures.Add(file.Replace(_packageRoot, ""));
            }

            foreach (var dir in Directory.GetDirectories(_packageRoot, "*", SearchOption.AllDirectories))
            {
                if (IsExcluded(dir)) continue;
                if (!File.Exists(dir + ".meta"))
                    failures.Add(dir.Replace(_packageRoot, "") + " (folder)");
            }

            Assert.IsEmpty(failures, $"Assets missing .meta files:\n{string.Join("\n", failures)}");
        }

        [Test]
        public void AllMetaFiles_HaveValidGuid()
        {
            var guidPattern = new Regex(@"^guid:\s+([0-9a-f]{32})\s*$", RegexOptions.Multiline);
            var failures = new List<string>();

            foreach (var metaFile in Directory.GetFiles(_packageRoot, "*.meta", SearchOption.AllDirectories))
            {
                if (IsExcluded(metaFile)) continue;
                if (!guidPattern.IsMatch(File.ReadAllText(metaFile)))
                    failures.Add(metaFile.Replace(_packageRoot, ""));
            }

            Assert.IsEmpty(failures, $".meta files with invalid or missing GUID:\n{string.Join("\n", failures)}");
        }

        [Test]
        public void NoOrphanedMetaFiles()
        {
            var failures = new List<string>();

            foreach (var metaFile in Directory.GetFiles(_packageRoot, "*.meta", SearchOption.AllDirectories))
            {
                if (IsExcluded(metaFile)) continue;
                var assetPath = metaFile.Substring(0, metaFile.Length - ".meta".Length);
                if (!File.Exists(assetPath) && !Directory.Exists(assetPath))
                    failures.Add(metaFile.Replace(_packageRoot, ""));
            }

            Assert.IsEmpty(failures, $"Orphaned .meta files (no corresponding asset):\n{string.Join("\n", failures)}");
        }

        private static bool IsExcluded(string path)
        {
            foreach (var part in path.Replace('\\', '/').Split('/'))
            {
                if (part.EndsWith("~") || part.StartsWith("."))
                    return true;
            }
            return false;
        }
    }
}
