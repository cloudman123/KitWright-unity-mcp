// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using KitWright.Editor.Tools;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    /// <summary>
    /// A ratchet, not a metric. It reads the test sources next to it and asks, for every registered
    /// tool, whether any test names it at all - by calling the method or by naming the tool in a
    /// string. Tools nothing names are listed in <see cref="KnownUntestedTools"/>; the test fails when
    /// a NEW tool joins them, so the surface can only get better covered from here. The baseline is
    /// also checked for staleness, so it shrinks as tests arrive instead of rotting.
    /// </summary>
    public sealed class ToolTestCoverageTests
    {
        // Tools with no test naming them. Delete a line when you write one - the staleness test below
        // fails if you do not. Do not add a line to make a red run green: write the test instead.
        private static readonly string[] KnownUntestedTools =
        {
            "add_assembly_references",
            "add_audio_listener",
            "add_audio_source",
            "add_constraint",
            "add_input_action",
            "add_input_binding",
            "add_input_composite_binding",
            "add_input_map",
            "add_layer",
            "add_nav_mesh_agent",
            "add_nav_mesh_obstacle",
            "add_sorting_layer",
            "add_tag",
            "add_terrain_layer",
            "add_to_sprite_atlas",
            "add_tree_prototype",
            "add_volume_override",
            "adjust_terrain_height",
            "align_view_to_object",
            "apply_gradient",
            "apply_noise",
            "apply_pattern",
            "apply_prefab_overrides",
            "assign_animator",
            "assign_material",
            "bake_lightmaps",
            "batch_execute",
            "cancel_test_run",
            "clear_console",
            "clear_execute_code_history",
            "clear_nav_mesh",
            "close_scene",
            "copy_asset",
            "create_assembly_def",
            "create_button",
            "create_canvas",
            "create_folder",
            "create_image",
            "create_input_actions",
            "create_lod_group",
            "create_material",
            "create_scriptable_object",
            "create_shader",
            "create_text",
            "create_texture",
            "delete_all_player_prefs",
            "delete_editor_pref",
            "delete_player_pref",
            "duplicate_game_object",
            "edit_script_members",
            "enter_play_mode",
            "exit_play_mode",
            "flatten_terrain",
            "frame_debugger_enable",
            "frame_object",
            "install_package",
            "instantiate_prefab",
            "load_scene_additive",
            "look_at_point",
            "mark_addressable",
            "memory_take_full_snapshot",
            "modify_build_scenes",
            "move_asset",
            "open_scene",
            "paint_terrain_layer",
            "place_terrain_trees",
            "play_animator_state",
            "play_clip_preview",
            "redo",
            "remove_assembly_references",
            "remove_component",
            "remove_from_sprite_atlas",
            "remove_layer",
            "remove_package",
            "remove_sorting_layer",
            "remove_tag",
            "remove_volume_override",
            "rename_asset",
            "rename_game_object",
            "rename_sorting_layer",
            "replay_execute_code",
            "reset_learned_modal_menu_items",
            "revert_prefab_overrides",
            "run_tests",
            "set_active",
            "set_active_scene",
            "set_active_tool",
            "set_addressable_address",
            "set_addressable_label",
            "set_agent_destination",
            "set_animator_parameter",
            "set_assembly_platforms",
            "set_camera_culling_mask",
            "set_camera_projection",
            "set_camera_settings",
            "set_component_enabled",
            "set_editor_pref",
            "set_global_audio",
            "set_paused",
            "set_player_pref",
            "set_rect_transform",
            "set_scene_view_camera",
            "set_selection",
            "set_sprite_atlas_settings",
            "set_tag_and_layer",
            "set_texture_as_sprite",
            "set_time_scale",
            "set_tool_profile",
            "set_volume_override_property",
            "simulate_editor_window_click",
            "simulate_editor_window_key",
            "slice_sprite_grid",
            "step_frame",
            "stop_clip_preview",
            "undo",
            "unmark_addressable",
            "unpack_prefab",
            "update_assembly_def_settings",
            "update_shader"
        };

        private static string ThisFile([CallerFilePath] string path = null) => path;

        private static string TestsRoot() => Path.GetDirectoryName(ThisFile());

        private static string TestSources()
        {
            var root = TestsRoot();
            if (root == null || !Directory.Exists(root))
                Assert.Ignore($"The test sources are not on disk at '{root}', so coverage cannot be read.");

            var self = ThisFile();
            var text = new StringBuilder();

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                // Reading this file would make every baselined name count as covered by its own baseline.
                if (string.Equals(file, self, StringComparison.OrdinalIgnoreCase))
                    continue;

                text.Append(File.ReadAllText(file)).Append('\n');
            }

            return text.ToString();
        }

        // A tool counts as named when a test calls it (Provider.Method) or asks for it by tool name
        // ("get_hierarchy"). Both are deliberate mentions; prose in a comment is not.
        private static List<string> UncoveredTools(string sources)
        {
            var uncovered = new List<string>();

            foreach (var entry in ToolRegistry.MethodCache.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (ToolRegistry.IsCustomTool(entry.Key))
                    continue;

                var qualified = $"{entry.Value.DeclaringType?.Name}.{entry.Value.Name}";
                if (sources.Contains(qualified) || sources.Contains($"\"{entry.Key}\""))
                    continue;

                uncovered.Add(entry.Key);
            }

            return uncovered;
        }

        [Test]
        public void NoToolArrivesWithoutATestNamingIt()
        {
            var uncovered = UncoveredTools(TestSources());
            var baseline = new HashSet<string>(KnownUntestedTools, StringComparer.Ordinal);
            var unlisted = uncovered.Where(name => !baseline.Contains(name)).ToList();

            if (unlisted.Count == 0)
                return;

            Assert.Fail($"{unlisted.Count} tool(s) that no test names, and that the baseline does not " +
                        $"account for. Write a test, or - if it genuinely cannot be tested here - paste " +
                        $"these into KnownUntestedTools:{Environment.NewLine}" +
                        string.Join(Environment.NewLine, unlisted.Select(name => $"            \"{name}\",")));
        }

        [Test]
        public void TheUntestedBaselineHasNoStaleEntries()
        {
            var sources = TestSources();
            var uncovered = new HashSet<string>(UncoveredTools(sources), StringComparer.Ordinal);
            var stale = new List<string>();

            foreach (var name in KnownUntestedTools)
            {
                if (ToolRegistry.GetMethod(name) == null)
                    stale.Add($"'{name}' is not a registered tool any more");
                else if (!uncovered.Contains(name))
                    stale.Add($"'{name}' is covered now - delete the line");
            }

            if (stale.Count == 0)
                return;

            stale.Sort(StringComparer.Ordinal);
            Assert.Fail($"{stale.Count} stale baseline entry/entries:{Environment.NewLine}  " +
                        string.Join(Environment.NewLine + "  ", stale));
        }

        [Test]
        public void TheCoverageReaderFindsTheTestSources()
        {
            // Guards the reader itself: a path that resolved to an empty folder would report every
            // tool as untested and, worse, would let the baseline swallow the whole surface.
            var sources = TestSources();

            Assert.Greater(sources.Length, 10000, "Read almost no test source, so coverage below is meaningless.");
            StringAssert.Contains("HierarchyFunctions.GetHierarchy", sources,
                "A known call is missing from the sources read, so the coverage signal is broken.");
        }
    }
}
