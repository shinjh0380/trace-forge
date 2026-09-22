using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

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

        [Test]
        public void PackageMetadata_UsesCanonicalRepositoryAndOwner()
        {
            var package = File.ReadAllText(Path.Combine(_packageRoot, "package.json"));
            var changelog = File.ReadAllText(Path.Combine(_packageRoot, "CHANGELOG.md"));

            Assert.IsTrue(HasCanonicalMetadata(package, changelog));
        }

        [Test]
        public void PackageVersion_MatchesFirstReleasedChangelogEntry()
        {
            var package = File.ReadAllText(Path.Combine(_packageRoot, "package.json"));
            var changelog = File.ReadAllText(Path.Combine(_packageRoot, "CHANGELOG.md"));
            var packageVersion = Regex.Match(package, "\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"").Groups[1].Value;
            var released = Regex.Match(changelog, @"^## \[(?!Unreleased\])([^\]]+)\]", RegexOptions.Multiline).Groups[1].Value;

            Assert.IsNotEmpty(packageVersion, "package.json version is missing");
            Assert.IsNotEmpty(released, "CHANGELOG has no released version");
            Assert.AreEqual(packageVersion, released);
        }

        [Test]
        public void MetadataValidation_ValidFixture_IsAccepted_AndSkipsUnreleased()
        {
            Assert.IsTrue(HasCanonicalMetadata(ValidPackageJson, ValidReleasedChangelog));
        }

        [TestCase("bad-host")]
        [TestCase("bad-owner")]
        [TestCase("bad-documentation-owner")]
        [TestCase("version-mismatch")]
        public void MetadataValidation_SingleFieldMutation_IsRejected(string mutation)
        {
            var metadata = JsonUtility.FromJson<PackageMetadata>(ValidPackageJson);
            switch (mutation)
            {
                case "bad-host":
                    metadata.documentationUrl = "https://example.com/shinjh0380/trace-forge";
                    break;
                case "bad-owner":
                    metadata.repository.url = "https://github.com/other-owner/trace-forge.git";
                    break;
                case "bad-documentation-owner":
                    metadata.changelogUrl = "https://github.com/other-owner/trace-forge/blob/main/CHANGELOG.md";
                    break;
                case "version-mismatch":
                    metadata.version = "0.1.0";
                    break;
            }

            Assert.IsFalse(HasCanonicalMetadata(JsonUtility.ToJson(metadata), ValidReleasedChangelog));
        }

        private static bool HasCanonicalMetadata(string package, string changelog)
        {
            if (string.IsNullOrEmpty(package) || string.IsNullOrEmpty(changelog))
                return false;

            var metadata = JsonUtility.FromJson<PackageMetadata>(package);
            var firstReleased = Regex.Match(changelog, @"^## \[(?!Unreleased\])([^\]]+)\]", RegexOptions.Multiline).Groups[1].Value;
            return metadata != null &&
                metadata.version == firstReleased &&
                IsRepositoryUrl(metadata.repository != null ? metadata.repository.url : null) &&
                IsDocumentationRootUrl(metadata.documentationUrl) &&
                IsDocumentationUrl(metadata.licensesUrl, "LICENSE.md") &&
                IsDocumentationUrl(metadata.changelogUrl, "CHANGELOG.md") &&
                metadata.author != null &&
                IsAuthorUrl(metadata.author.url);
        }

        private static bool IsRepositoryUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host == "github.com" &&
                uri.AbsolutePath.Trim('/').Equals("shinjh0380/trace-forge.git", System.StringComparison.Ordinal);
        }

        private static bool IsDocumentationUrl(string value, string fileName)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host == "github.com" &&
                uri.AbsolutePath.Trim('/').Equals("shinjh0380/trace-forge/blob/main/" + fileName, System.StringComparison.Ordinal);
        }

        private static bool IsAuthorUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host == "github.com" &&
                uri.AbsolutePath.Trim('/').Equals("shinjh0380/trace-forge", System.StringComparison.Ordinal);
        }

        private const string ValidPackageJson = "{\"version\":\"0.2.0\",\"documentationUrl\":\"https://github.com/shinjh0380/trace-forge\",\"changelogUrl\":\"https://github.com/shinjh0380/trace-forge/blob/main/CHANGELOG.md\",\"licensesUrl\":\"https://github.com/shinjh0380/trace-forge/blob/main/LICENSE.md\",\"author\":{\"url\":\"https://github.com/shinjh0380/trace-forge\"},\"repository\":{\"url\":\"https://github.com/shinjh0380/trace-forge.git\"}}";
        private const string ValidReleasedChangelog = "## [Unreleased]\n\n## [0.2.0] - 2026-03-07";

        private static bool IsDocumentationRootUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                uri.Host == "github.com" &&
                uri.AbsolutePath.Trim('/').Equals("shinjh0380/trace-forge", StringComparison.Ordinal);
        }

        [System.Serializable]
        private sealed class PackageMetadata
        {
            public string version = string.Empty;
            public string documentationUrl = string.Empty;
            public string changelogUrl = string.Empty;
            public string licensesUrl = string.Empty;
            public PackageAuthor author = new PackageAuthor();
            public PackageRepository repository = new PackageRepository();
        }

        [System.Serializable]
        private sealed class PackageAuthor
        {
            public string url = string.Empty;
        }

        [System.Serializable]
        private sealed class PackageRepository
        {
            public string url = string.Empty;
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
