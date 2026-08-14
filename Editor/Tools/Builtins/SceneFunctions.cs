// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using KitWright.Editor.Tools.Helpers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KitWright.Editor.Tools.Builtins
{
    [ToolProvider("Scene")]
    internal static class SceneFunctions
    {
        [Description("Save the current scene")]
        public static string SaveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            bool saved = EditorSceneManager.SaveScene(scene);
            return saved ? $"Saved scene '{scene.name}'" : ToolResultFormatter.Error("SCENE_SAVE_FAILED", new { scene = scene.name });
        }

        // Never call SaveCurrentModifiedScenesIfUserWantsTo: its modal dialog blocks the editor
        // main loop, which stalls the MCP request pump until a human clicks a button.
        private static string UnsavedChangesError(bool discardUnsaved)
        {
            if (discardUnsaved)
                return null;

            var dirty = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty)
                    dirty.Add(string.IsNullOrEmpty(s.path) ? s.name : s.path);
            }

            if (dirty.Count == 0)
                return null;

            return ToolResultFormatter.Error("SCENE_HAS_UNSAVED_CHANGES", new
            {
                scenes = dirty.ToArray(),
                hint = "Call save_scene first, or pass discard_unsaved=true to drop the changes."
            });
        }

        [Description("Open an existing scene by path. Fails if any open scene has unsaved changes unless discard_unsaved is true.")]
        public static string OpenScene(
            [ToolParam("Path to the scene asset (e.g. 'Assets/Scenes/Main.unity')")] string path,
            [ToolParam("Drop unsaved changes in the currently open scenes instead of failing", Required = false)] bool discard_unsaved = false)
        {
            if (!System.IO.File.Exists(path))
                return ToolResultFormatter.Error("SCENE_FILE_NOT_FOUND", new { path });

            var blocked = UnsavedChangesError(discard_unsaved);
            if (blocked != null)
                return blocked;

            EditorSceneManager.OpenScene(path);
            return $"Opened scene: {path}";
        }

        [Description("Open a scene additively (keeps currently open scenes loaded), for multi-scene editing. Use set_active_scene to make it the active scene afterward.")]
        public static string LoadSceneAdditive(
            [ToolParam("Path to the scene asset (e.g. 'Assets/Scenes/Enemies.unity')")] string path)
        {
            if (!System.IO.File.Exists(path))
                return ToolResultFormatter.Error("SCENE_FILE_NOT_FOUND", new { path });

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            return $"Loaded scene additively: {scene.name} ({SceneManager.sceneCount} scene(s) open)";
        }

        [Description("Set which open scene is the active scene (new objects go here; lighting/nav settings follow it). The scene must already be open. Identify by name or path.")]
        public static string SetActiveScene(
            [ToolParam("Open scene name or path")] string scene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == scene || s.path == scene)
                {
                    if (!s.isLoaded)
                        return ToolResultFormatter.Error("SCENE_NOT_LOADED", new { scene });
                    SceneManager.SetActiveScene(s);
                    return $"Active scene: {s.name}";
                }
            }
            return ToolResultFormatter.Error("SCENE_NOT_OPEN", new { scene, hint = "Open it first (open_scene or load_scene_additive)." });
        }

        [Description("Close/unload an open scene (used in multi-scene editing). Cannot close the only open scene. Identify by name or path; optionally remove it from the Hierarchy entirely. Fails if that scene has unsaved changes unless discard_unsaved is true.")]
        public static string CloseScene(
            [ToolParam("Open scene name or path")] string scene,
            [ToolParam("Remove the scene from the Hierarchy (true) or just unload it (false)", Required = false)] bool remove = true,
            [ToolParam("Drop unsaved changes in that scene instead of failing", Required = false)] bool discard_unsaved = false)
        {
            if (SceneManager.sceneCount <= 1)
                return ToolResultFormatter.Error("CANNOT_CLOSE_LAST_SCENE", new { hint = "At least one scene must stay open." });

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == scene || s.path == scene)
                {
                    if (s.isDirty && !discard_unsaved)
                        return ToolResultFormatter.Error("SCENE_HAS_UNSAVED_CHANGES", new
                        {
                            scene = s.path,
                            hint = "Call save_scene first, or pass discard_unsaved=true to drop the changes."
                        });

                    EditorSceneManager.CloseScene(s, remove);
                    return $"Closed scene: {scene}";
                }
            }
            return ToolResultFormatter.Error("SCENE_NOT_OPEN", new { scene });
        }

        [Description("Create a new empty scene. Fails if any open scene has unsaved changes unless discard_unsaved is true.")]
        public static string CreateNewScene(
            [ToolParam("Name for the new scene")] string name,
            [ToolParam("Path to save (e.g. 'Assets/Scenes/')", Required = false)] string save_path = "Assets/Scenes/",
            [ToolParam("Drop unsaved changes in the currently open scenes instead of failing", Required = false)] bool discard_unsaved = false)
        {
            var blocked = UnsavedChangesError(discard_unsaved);
            if (blocked != null)
                return blocked;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            if (!System.IO.Directory.Exists(save_path))
                System.IO.Directory.CreateDirectory(save_path);

            var fullPath = $"{save_path}{name}.unity";
            EditorSceneManager.SaveScene(scene, fullPath);
            return $"Created and saved new scene: {fullPath}";
        }

        [Description("Get information about every loaded scene (the active scene plus any additively loaded ones), " +
                     "including path, dirty state, and a shallow root-object hierarchy per scene.")]
        [ReadOnlyTool]
        public static string GetSceneInfo()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                sb.AppendLine(scene == activeScene ? $"Scene: {scene.name} (active)" : $"Scene: {scene.name} (additive)");
                sb.AppendLine($"Path: {scene.path}");
                sb.AppendLine($"Is Dirty: {scene.isDirty}");
                sb.AppendLine($"Root Objects ({rootObjects.Length}):");

                foreach (var go in rootObjects)
                {
                    AppendHierarchy(sb, go.transform, 1, 3);
                }
            }

            return sb.ToString();
        }

        [Description("List all scenes in the project")]
        [ReadOnlyTool]
        public static string ListScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Scene");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Found {guids.Length} scenes:");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                sb.AppendLine($"  - {path}");
            }

            return sb.ToString();
        }

        [Description("Enter play mode in the editor")]
        public static string EnterPlayMode()
        {
            if (EditorApplication.isPlaying)
                return "Already in play mode";

            EditorApplication.isPlaying = true;
            return "Entering play mode";
        }

        [Description("Exit play mode in the editor")]
        public static string ExitPlayMode()
        {
            if (!EditorApplication.isPlaying)
                return "Not in play mode";

            EditorApplication.isPlaying = false;
            return "Exiting play mode";
        }

        [Description("Pause or resume play mode. Requires being in play mode. Use step_frame to advance one frame while paused.")]
        public static string SetPaused(
            [ToolParam("true to pause, false to resume")] bool paused)
        {
            if (!EditorApplication.isPlaying)
                return ToolResultFormatter.Error("NOT_IN_PLAY_MODE", new { hint = "Enter play mode first." });

            EditorApplication.isPaused = paused;
            return paused ? "Paused play mode" : "Resumed play mode";
        }

        [Description("Advance play mode by exactly one frame. Auto-pauses if running. Requires being in play mode. Useful for frame-by-frame debugging.")]
        public static string StepFrame()
        {
            if (!EditorApplication.isPlaying)
                return ToolResultFormatter.Error("NOT_IN_PLAY_MODE", new { hint = "Enter play mode first." });

            EditorApplication.isPaused = true;
            EditorApplication.Step();
            return "Stepped one frame";
        }

        [Description("Set the game time scale. Use 0 to pause, 1 for normal speed, " +
                     "2 for double speed, etc. Useful for testing or slow-motion debugging.")]
        public static string SetTimeScale(
            [ToolParam("Time scale value (0=paused, 1=normal, 2=double speed, etc.)")] float scale)
        {
            if (scale < 0f)
                return ToolResultFormatter.Error("INVALID_TIME_SCALE", new { scale, min = 0f });
            if (scale > 100f)
                return ToolResultFormatter.Error("INVALID_TIME_SCALE", new { scale, max = 100f });

            float previousScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = scale;
            return $"Time.timeScale changed from {previousScale:F2} to {scale:F2}";
        }

        [Description("Get the current time scale and time information")]
        [ReadOnlyTool]
        public static string GetTimeScale()
        {
            return $"Time.timeScale={UnityEngine.Time.timeScale:F2}, Time.time={UnityEngine.Time.time:F2}, " +
                   $"Time.deltaTime={UnityEngine.Time.deltaTime:F4}, Time.fixedDeltaTime={UnityEngine.Time.fixedDeltaTime:F4}";
        }

        private static void AppendHierarchy(System.Text.StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;
            var indent = new string(' ', depth * 2);
            var components = t.GetComponents<Component>();
            var compNames = new System.Collections.Generic.List<string>();
            foreach (var c in components)
            {
                if (c != null && !(c is Transform))
                    compNames.Add(c.GetType().Name);
            }
            var compStr = compNames.Count > 0 ? $" [{string.Join(", ", compNames)}]" : "";
            sb.AppendLine($"{indent}- {t.name}{compStr}");

            for (int i = 0; i < t.childCount; i++)
            {
                AppendHierarchy(sb, t.GetChild(i), depth + 1, maxDepth);
            }
        }
    }
}
