// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.IO;
using KitWright.Editor.Tools.Builtins;
using KitWright.Editor.Tools.Helpers;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KitWright.Editor.Tests
{
    public sealed class HierarchyFunctionsTests
    {
        [Test]
        public void FindTarget_ResolvesInactiveObjectsByNamePathAndInstanceId()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var scene = SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            GameObject firstRoot = null;
            GameObject secondRoot = null;
            GameObject inactiveByName = null;

            try
            {
                firstRoot = new GameObject("FirstRoot_" + suffix);
                secondRoot = new GameObject("SecondRoot_" + suffix);
                var duplicateName = "Duplicate_" + suffix;
                var firstChild = new GameObject(duplicateName);
                var secondChild = new GameObject(duplicateName);
                inactiveByName = new GameObject("Inactive_" + suffix);

                firstChild.transform.SetParent(firstRoot.transform);
                secondChild.transform.SetParent(secondRoot.transform);
                firstChild.SetActive(false);
                inactiveByName.SetActive(false);

                Assert.AreSame(inactiveByName, ObjectsHelper.FindTarget(inactiveByName.name));
                Assert.AreSame(firstChild, ObjectsHelper.FindTarget(firstRoot.name + "/" + duplicateName));
                Assert.AreSame(secondChild, ObjectsHelper.FindTarget(secondRoot.name + "/" + duplicateName));
                Assert.AreSame(firstChild, ObjectsHelper.FindTarget(ObjectIdHelper.GetSerializableId(firstChild)));
            }
            finally
            {
                if (firstRoot != null) UnityEngine.Object.DestroyImmediate(firstRoot);
                if (secondRoot != null) UnityEngine.Object.DestroyImmediate(secondRoot);
                if (inactiveByName != null) UnityEngine.Object.DestroyImmediate(inactiveByName);
                if (!wasDirty && scene.IsValid())
                {
                    var clearDirtiness = typeof(EditorSceneManager).GetMethod(
                        "ClearSceneDirtiness",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    clearDirtiness?.Invoke(null, new object[] { scene });
                }
            }
        }

        [Test]
        public void HierarchyAndSceneInfo_IncludeLoadedAdditiveScenes()
        {
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            bool canRestoreOriginalSetup = CanRestoreSceneSetup(originalSetup);
            if (!Application.isBatchMode && !canRestoreOriginalSetup)
                Assert.Ignore("Skipping additive-scene test because the interactive editor has unsaved untitled scenes.");

            Scene additiveScene = default;

            string suffix = Guid.NewGuid().ToString("N");
            string tempFolder = "Assets/__KitWrightMcpSceneHierarchyTests";
            string activeScenePath = tempFolder + "/Active_" + suffix + ".unity";
            string additiveScenePath = tempFolder + "/Additive_" + suffix + ".unity";
            string activeRootName = "KitWrightActiveRoot_" + suffix;
            string additiveRootName = "KitWrightAdditiveRoot_" + suffix;
            string inactiveRootName = "KitWrightInactiveAdditiveRoot_" + suffix;

            try
            {
                EnsureFolder(tempFolder);

                var activeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.IsTrue(EditorSceneManager.SaveScene(activeScene, activeScenePath));
                new GameObject(activeRootName);

                additiveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                Assert.IsTrue(additiveScene.IsValid());
                Assert.IsTrue(EditorSceneManager.SaveScene(additiveScene, additiveScenePath));
                var additiveRoot = new GameObject(additiveRootName);
                SceneManager.MoveGameObjectToScene(additiveRoot, additiveScene);
                var inactiveRoot = new GameObject(inactiveRootName);
                SceneManager.MoveGameObjectToScene(inactiveRoot, additiveScene);
                inactiveRoot.SetActive(false);

                Assert.IsTrue(SceneManager.SetActiveScene(activeScene));

                var hierarchy = HierarchyFunctions.GetHierarchy(
                    depth: 1,
                    include_components: false,
                    include_inactive: true);

                Assert.That(hierarchy, Does.Contain("Scene: " + activeScene.name));
                Assert.That(hierarchy, Does.Contain(activeRootName));
                Assert.That(hierarchy, Does.Contain("Scene: " + additiveScene.name + " (additive)"));
                Assert.That(hierarchy, Does.Contain(additiveRootName));
                Assert.That(hierarchy, Does.Contain(inactiveRootName + " [INACTIVE]"));

                var rootLookup = HierarchyFunctions.GetHierarchy(
                    root_name: inactiveRootName,
                    depth: 1,
                    include_components: false,
                    include_inactive: true);

                Assert.That(rootLookup, Does.Contain(inactiveRootName + " [INACTIVE]"));
                Assert.That(rootLookup, Does.Not.Contain("GAME_OBJECT_NOT_FOUND"));

                var sceneInfo = SceneFunctions.GetSceneInfo();
                Assert.That(sceneInfo, Does.Contain("Scene: " + activeScene.name + " (active)"));
                Assert.That(sceneInfo, Does.Contain(activeRootName));
                Assert.That(sceneInfo, Does.Contain("Scene: " + additiveScene.name + " (additive)"));
                Assert.That(sceneInfo, Does.Contain(additiveRootName));
                Assert.That(sceneInfo, Does.Contain(inactiveRootName));
            }
            finally
            {
                if (canRestoreOriginalSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
                else if (Application.isBatchMode)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                }

                if (AssetDatabase.IsValidFolder(tempFolder))
                    AssetDatabase.DeleteAsset(tempFolder);
            }
        }

        // ------------------------------------------------------------------
        //  Edge cases: GetHierarchy boundary conditions
        // ------------------------------------------------------------------

        [Test]
        public void GetHierarchy_DepthZero_ClampedToOneStillReturnsHierarchy()
        {
            // depth=0 gets clamped to 1 by Mathf.Clamp(depth, 1, 10)
            var result = HierarchyFunctions.GetHierarchy(depth: 0);

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            // Should contain Scene header since it's a full hierarchy call
            Assert.That(result, Does.Contain("Scene:"));
        }

        [Test]
        public void GetHierarchy_NegativeDepth_ClampedToOneStillReturnsHierarchy()
        {
            // depth=-1 gets clamped to 1 by Mathf.Clamp(depth, 1, 10)
            var result = HierarchyFunctions.GetHierarchy(depth: -1);

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            Assert.That(result, Does.Contain("Scene:"));
        }

        [Test]
        public void GetHierarchy_DepthExceedsMax_ClampedToTen()
        {
            // depth=100 gets clamped to 10 by Mathf.Clamp(depth, 1, 10)
            var result = HierarchyFunctions.GetHierarchy(depth: 100);

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            Assert.That(result, Does.Contain("Scene:"));
        }

        [Test]
        public void GetHierarchy_NonexistentRootName_ReturnsGameObjectNotFoundError()
        {
            var result = HierarchyFunctions.GetHierarchy(
                root_name: "NonExistent_Object_" + Guid.NewGuid().ToString("N"));

            StringAssert.Contains("GAME_OBJECT_NOT_FOUND", result);
            StringAssert.Contains("\"success\":false", result);
        }

        [Test]
        public void GetHierarchy_ExcludeInactive_DoesNotShowInactiveObjects()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var scene = SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            GameObject activeObj = null;
            GameObject inactiveObj = null;

            try
            {
                activeObj = new GameObject("ActiveTest_" + suffix);
                inactiveObj = new GameObject("InactiveTest_" + suffix);
                inactiveObj.SetActive(false);

                var result = HierarchyFunctions.GetHierarchy(
                    depth: 1,
                    include_components: false,
                    include_inactive: false);

                Assert.That(result, Does.Contain("ActiveTest_" + suffix));
                Assert.That(result, Does.Not.Contain("InactiveTest_" + suffix));
            }
            finally
            {
                if (activeObj != null) UnityEngine.Object.DestroyImmediate(activeObj);
                if (inactiveObj != null) UnityEngine.Object.DestroyImmediate(inactiveObj);
                if (!wasDirty && scene.IsValid())
                {
                    var clearDirtiness = typeof(EditorSceneManager).GetMethod(
                        "ClearSceneDirtiness",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    clearDirtiness?.Invoke(null, new object[] { scene });
                }
            }
        }

        [Test]
        public void GetHierarchy_IncludeInactive_ShowsInactiveObjectsWithMarker()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var scene = SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            GameObject inactiveObj = null;

            try
            {
                inactiveObj = new GameObject("InactiveMarkerTest_" + suffix);
                inactiveObj.SetActive(false);

                var result = HierarchyFunctions.GetHierarchy(
                    depth: 1,
                    include_components: false,
                    include_inactive: true);

                Assert.That(result, Does.Contain("InactiveMarkerTest_" + suffix + " [INACTIVE]"));
            }
            finally
            {
                if (inactiveObj != null) UnityEngine.Object.DestroyImmediate(inactiveObj);
                if (!wasDirty && scene.IsValid())
                {
                    var clearDirtiness = typeof(EditorSceneManager).GetMethod(
                        "ClearSceneDirtiness",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    clearDirtiness?.Invoke(null, new object[] { scene });
                }
            }
        }

        [Test]
        public void GetHierarchy_IncludeComponents_ShowsComponentNames()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var scene = SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            GameObject testObj = null;

            try
            {
                testObj = new GameObject("ComponentTest_" + suffix);
                testObj.AddComponent<BoxCollider>();

                var withComponents = HierarchyFunctions.GetHierarchy(
                    root_name: "ComponentTest_" + suffix,
                    depth: 1,
                    include_components: true);

                var withoutComponents = HierarchyFunctions.GetHierarchy(
                    root_name: "ComponentTest_" + suffix,
                    depth: 1,
                    include_components: false);

                Assert.That(withComponents, Does.Contain("BoxCollider"));
                Assert.That(withoutComponents, Does.Not.Contain("BoxCollider"));
            }
            finally
            {
                if (testObj != null) UnityEngine.Object.DestroyImmediate(testObj);
                if (!wasDirty && scene.IsValid())
                {
                    var clearDirtiness = typeof(EditorSceneManager).GetMethod(
                        "ClearSceneDirtiness",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    clearDirtiness?.Invoke(null, new object[] { scene });
                }
            }
        }

        [Test]
        public void GetHierarchy_RootNameByHierarchyPath_ReturnsSubtreeOnly()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var scene = SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            GameObject parent = null;

            try
            {
                parent = new GameObject("PathParent_" + suffix);
                var child = new GameObject("PathChild_" + suffix);
                child.transform.SetParent(parent.transform);

                var result = HierarchyFunctions.GetHierarchy(
                    root_name: "PathParent_" + suffix + "/PathChild_" + suffix,
                    depth: 1,
                    include_components: false);

                Assert.That(result, Does.Contain("PathChild_" + suffix));
                Assert.That(result, Does.Not.Contain("GAME_OBJECT_NOT_FOUND"));
            }
            finally
            {
                if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
                if (!wasDirty && scene.IsValid())
                {
                    var clearDirtiness = typeof(EditorSceneManager).GetMethod(
                        "ClearSceneDirtiness",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic);
                    clearDirtiness?.Invoke(null, new object[] { scene });
                }
            }
        }

        private static bool CanRestoreSceneSetup(SceneSetup[] setup)
        {
            foreach (var scene in setup)
            {
                if (string.IsNullOrEmpty(scene.path) || !File.Exists(scene.path))
                    return false;
            }

            return setup.Length > 0;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException("Temporary test folder must be under Assets.");

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
