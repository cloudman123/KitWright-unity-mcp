// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEditor;

namespace KitWright.Editor.Tests
{
    // search_files and list_directory both used to stop at a hardcoded cap with no way to reach
    // the rest, so a folder past the cap was simply invisible to an agent.
    public sealed class FileListingPagingTests
    {
        private const string FolderName = "__KitWrightFileListingPagingTests";
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/" + FolderName;
            if (!AssetDatabase.IsValidFolder(_folder))
                AssetDatabase.CreateFolder("Assets", FolderName);

            for (var i = 0; i < 3; i++)
                File.WriteAllText($"{_folder}/paged_{i}.txt", "x");
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
                AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void SearchFiles_PagesJoinIntoTheSameListAsOneWholeRead()
        {
            var whole = Entries(FileFunctions.SearchFiles("paged_*.txt", _folder, max: 50));
            Assert.AreEqual(3, whole.Count, "Setup failed: the three files are not searchable.");

            var first = FileFunctions.SearchFiles("paged_*.txt", _folder, max: 2);
            StringAssert.Contains("Found 3 files.", first);
            StringAssert.Contains("Showing 1-2 of 3; pass cursor=2", first);

            var second = FileFunctions.SearchFiles("paged_*.txt", _folder, max: 2, cursor: 2);
            StringAssert.Contains("end of the list", second);

            CollectionAssert.AreEqual(whole, Entries(first).Concat(Entries(second)).ToList());
        }

        [Test]
        public void ListDirectory_PagesTheFilesAndAlwaysKeepsTheSubdirectories()
        {
            AssetDatabase.CreateFolder(_folder, "sub");

            var first = FileFunctions.ListDirectory(_folder, max: 2);
            StringAssert.Contains("Showing 1-2 of 3; pass cursor=2", first);
            StringAssert.Contains("[DIR] sub/", first);

            var second = FileFunctions.ListDirectory(_folder, max: 2, cursor: 2);
            StringAssert.Contains("end of the list", second);
            StringAssert.Contains("paged_2.txt", second);
            Assert.That(second, Does.Not.Contain("paged_0.txt"),
                "Page two repeated page one, so the cursor was ignored.");
        }

        private static List<string> Entries(string response) =>
            response.Split('\n')
                .Where(line => line.TrimStart().StartsWith("- "))
                .Select(line => line.Trim().Substring(2))
                .ToList();
    }
}
