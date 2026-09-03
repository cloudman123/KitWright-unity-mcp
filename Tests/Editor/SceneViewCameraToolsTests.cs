// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using KitWright.Editor.Tools;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    /// <summary>
    /// The four Scene View camera writers. A batchmode run has no Scene View window at all, so the
    /// contract checked there is that each tool says so instead of throwing; run from an open editor
    /// the same test reads the camera back and the view is put where it was afterwards.
    /// </summary>
    public sealed class SceneViewCameraToolsTests
    {
        private const string Subject = "KwSceneViewSubject";
        private const string NoView = "NO_SCENE_VIEW";

        private static JObject Call(string tool, params string[] pairs)
        {
            var parameters = new Dictionary<string, string>();
            for (var i = 0; i + 1 < pairs.Length; i += 2)
                parameters[pairs[i]] = pairs[i + 1];

            return JObject.Parse(new FunctionInvoker().Invoke(
                new FunctionCall { FunctionName = tool, Parameters = parameters }));
        }

        private static JObject Ok(string tool, params string[] pairs)
        {
            var answer = Call(tool, pairs);
            Assert.IsTrue((bool)answer["success"], $"{tool}: {answer}");
            return answer;
        }

        private static string Code(string tool, params string[] pairs) => (string)Call(tool, pairs)["code"];

        private static Vector3 Pivot(JObject answer) => new Vector3(
            (float)answer["data"]["pivot"]["x"],
            (float)answer["data"]["pivot"]["y"],
            (float)answer["data"]["pivot"]["z"]);

        private static string Vector(JToken point) =>
            $"{(float)point["x"]},{(float)point["y"]},{(float)point["z"]}";

        [Test]
        public void TheSceneViewToolsMoveTheCameraOrSayThereIsNoSceneView()
        {
            var before = Call("get_scene_view_camera");
            if ((string)before["code"] == NoView)
            {
                // No window to drive. All four still have to answer, and none of them may throw:
                // a NO_SCENE_VIEW answer is the tool working, a FUNCTION_FAILED is not.
                Assert.AreEqual(NoView, Code("set_scene_view_camera", "pivot", "1,2,3"));
                Assert.AreEqual(NoView, Code("frame_object", "target", "Main Camera"));
                Assert.AreEqual(NoView, Code("align_view_to_object", "target", "Main Camera"));
                Assert.AreEqual(NoView, Code("look_at_point", "point", "1,2,3"));
                return;
            }

            var subject = new GameObject(Subject);
            subject.transform.position = new Vector3(12f, 3f, -4f);

            try
            {
                // The only one of the four that writes the camera directly, so it is the only one whose
                // result can be read back in the same call: LookAt, FrameSelected and AlignViewToObject
                // all hand the camera to Unity's animated transition, which has not moved yet when the
                // tool describes the view. Those three are checked for answering, not for arriving.
                var placed = Ok("set_scene_view_camera", "pivot", "1,2,3", "rotation", "30,45,0", "size", "12");
                Assert.AreEqual(new Vector3(1f, 2f, 3f), Pivot(placed));
                Assert.AreEqual(12f, (float)placed["data"]["size"], 0.01f);

                Ok("look_at_point", "point", "5,5,5", "distance", "9");
                Ok("frame_object", "target", Subject);
                Ok("align_view_to_object", "target", Subject);

                Assert.AreEqual("GAME_OBJECT_NOT_FOUND", Code("frame_object", "target", "KwNothingCalledThis"));
                Assert.AreEqual("GAME_OBJECT_NOT_FOUND", Code("align_view_to_object", "target", "KwNothingCalledThis"));
                Assert.AreEqual("INVALID_VECTOR", Code("look_at_point", "point", "over there"));
            }
            finally
            {
                Object.DestroyImmediate(subject);

                // Whoever is running this from an open editor gets their view back.
                Call("set_scene_view_camera",
                    "pivot", Vector(before["data"]["pivot"]),
                    "rotation", Vector(before["data"]["rotation"]),
                    "size", ((float)before["data"]["size"]).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
