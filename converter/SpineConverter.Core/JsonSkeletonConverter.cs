using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace SpineConverter.Core;

public sealed class JsonSkeletonConverter
{
    private static readonly JsonSerializerOptions OutputOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public string Convert(string sourceJson, SpineVersion targetVersion)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(sourceJson)?.AsObject()
                ?? throw new ConversionException("The input JSON root must be an object.");
        }
        catch (JsonException exception)
        {
            throw new ConversionException($"Invalid JSON: {exception.Message}");
        }

        var skeleton = Object(root, "skeleton", "Missing required skeleton metadata.");
        var sourceText = skeleton["spine"]?.GetValue<string>()
            ?? throw new ConversionException("Missing required skeleton.spine version.");
        var sourceVersion = SpineVersion.Parse(sourceText);
        if (!sourceVersion.IsLine(3, 8) || !targetVersion.IsLine(4, 2))
            throw new ConversionException(
                $"This development build supports JSON 3.8 -> 4.2 only, not {sourceVersion} -> {targetVersion}.");

        ValidateSkins(root);
        ConvertTransformConstraints(root);
        ConvertPathConstraints(root);
        ConvertAnimations(root);
        skeleton["spine"] = targetVersion.ToString();
        return root.ToJsonString(OutputOptions);
    }

    private static void ValidateSkins(JsonObject root)
    {
        if (root["skins"] is null)
            return;
        if (root["skins"] is not JsonArray skins)
            throw new ConversionException("Spine 3.8 skins must be an array.");
        foreach (var node in skins)
        {
            if (node is not JsonObject skin || skin["name"] is null)
                throw new ConversionException("Every skin must be an object with a name.");
            if (skin["attachments"] is not null && skin["attachments"] is not JsonObject)
                throw new ConversionException($"Skin '{Text(skin, "name")}' has invalid attachments.");
        }
    }

    private static void ConvertTransformConstraints(JsonObject root)
    {
        if (root["transform"] is not JsonArray constraints)
            return;
        foreach (var node in constraints)
        {
            if (node is not JsonObject constraint)
                throw new ConversionException("Transform constraints must be objects.");
            Rename(constraint, "rotateMix", "mixRotate");
            Rename(constraint, "translateMix", "mixX");
            Rename(constraint, "scaleMix", "mixScaleX");
            Rename(constraint, "shearMix", "mixShearY");
        }
    }

    private static void ConvertPathConstraints(JsonObject root)
    {
        if (root["path"] is not JsonArray constraints)
            return;
        foreach (var node in constraints)
        {
            if (node is not JsonObject constraint)
                throw new ConversionException("Path constraints must be objects.");
            Rename(constraint, "rotateMix", "mixRotate");
            Rename(constraint, "translateMix", "mixX");
        }
    }

    private static void ConvertAnimations(JsonObject root)
    {
        if (root["animations"] is not JsonObject animations)
            return;
        foreach (var (animationName, node) in animations)
        {
            if (node is not JsonObject animation)
                throw new ConversionException($"Animation '{animationName}' must be an object.");
            ConvertBoneTimelines(animation);
            ConvertSlotTimelines(animation);
            ConvertIkTimelines(animation);
            ConvertTransformTimelines(animation);
            ConvertPathTimelines(animation);
            ConvertDeformTimelines(animation);
        }
    }

    private static void ConvertBoneTimelines(JsonObject animation)
    {
        if (animation["bones"] is not JsonObject bones)
            return;
        foreach (var (_, boneNode) in bones)
        {
            if (boneNode is not JsonObject bone)
                throw new ConversionException("Bone timeline entries must be objects.");
            foreach (var (timelineName, timelineNode) in bone.ToList())
            {
                if (timelineNode is not JsonArray frames)
                    throw new ConversionException($"Bone timeline '{timelineName}' must be an array.");
                switch (timelineName)
                {
                    case "rotate":
                        UnwrapRotation(frames);
                        ConvertCurves(frames, ["value"], [0]);
                        break;
                    case "translate":
                    case "translatex":
                    case "translatey":
                    case "shear":
                        ConvertCurves(frames, ChannelKeys(timelineName), Zeroes(ChannelKeys(timelineName).Length));
                        break;
                    case "scale":
                    case "scalex":
                    case "scaley":
                        ConvertCurves(frames, ChannelKeys(timelineName), Ones(ChannelKeys(timelineName).Length));
                        break;
                    default:
                        throw new ConversionException($"Unsupported Spine 3.8 bone timeline: {timelineName}");
                }
            }
        }
    }

    private static string[] ChannelKeys(string timelineName) => timelineName switch
    {
        "translatex" or "scalex" => ["x"],
        "translatey" or "scaley" => ["y"],
        _ => ["x", "y"],
    };

    private static void UnwrapRotation(JsonArray frames)
    {
        double previousSource = 0;
        double previousOutput = 0;
        var first = true;
        foreach (var node in frames)
        {
            var frame = node?.AsObject() ?? throw new ConversionException("Rotation frames must be objects.");
            var source = Number(frame, "angle", 0);
            var output = source;
            if (!first)
                output = previousOutput + NormalizeDegrees(source - previousSource);
            frame.Remove("angle");
            if (output != 0)
                frame["value"] = output;
            previousSource = source;
            previousOutput = output;
            first = false;
        }
    }

    private static double NormalizeDegrees(double value)
    {
        value %= 360;
        if (value > 180) value -= 360;
        if (value < -180) value += 360;
        return value;
    }

    private static void ConvertSlotTimelines(JsonObject animation)
    {
        if (animation["slots"] is not JsonObject slots)
            return;
        foreach (var (_, slotNode) in slots)
        {
            if (slotNode is not JsonObject slot)
                throw new ConversionException("Slot timeline entries must be objects.");
            Rename(slot, "color", "rgba");
            Rename(slot, "twoColor", "rgba2");
            foreach (var (timelineName, timelineNode) in slot)
            {
                if (timelineNode is not JsonArray frames)
                    throw new ConversionException($"Slot timeline '{timelineName}' must be an array.");
                switch (timelineName)
                {
                    case "attachment":
                        break;
                    case "rgba":
                        ConvertColorCurves(frames, false);
                        break;
                    case "rgba2":
                        ConvertColorCurves(frames, true);
                        break;
                    default:
                        throw new ConversionException($"Unsupported Spine 3.8 slot timeline: {timelineName}");
                }
            }
        }
    }

    private static void ConvertColorCurves(JsonArray frames, bool twoColor)
    {
        for (var index = 0; index + 1 < frames.Count; index++)
        {
            var frame = frames[index]?.AsObject() ?? throw new ConversionException("Color frames must be objects.");
            if (!IsBezier(frame))
                continue;
            var next = frames[index + 1]?.AsObject() ?? throw new ConversionException("Color frames must be objects.");
            var currentValues = ParseColor(Text(frame, twoColor ? "light" : "color"), true).ToList();
            var nextValues = ParseColor(Text(next, twoColor ? "light" : "color"), true).ToList();
            if (twoColor)
            {
                currentValues.AddRange(ParseColor(Text(frame, "dark"), false));
                nextValues.AddRange(ParseColor(Text(next, "dark"), false));
            }
            SetAbsoluteCurve(frame, next, currentValues, nextValues);
        }
    }

    private static IEnumerable<double> ParseColor(string hex, bool alpha)
    {
        var expected = alpha ? 8 : 6;
        if (hex.Length != expected)
            throw new ConversionException($"Invalid {(alpha ? "RGBA" : "RGB")} color: {hex}");
        for (var i = 0; i < expected; i += 2)
            yield return int.Parse(hex.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
    }

    private static void ConvertTransformTimelines(JsonObject animation)
    {
        if (animation["transform"] is not JsonObject timelines)
            return;
        foreach (var (_, timelineNode) in timelines)
        {
            if (timelineNode is not JsonArray frames)
                throw new ConversionException("Transform constraint timelines must be arrays.");
            foreach (var node in frames)
            {
                var frame = node?.AsObject() ?? throw new ConversionException("Transform frames must be objects.");
                Rename(frame, "rotateMix", "mixRotate");
                Rename(frame, "translateMix", "mixX");
                Rename(frame, "scaleMix", "mixScaleX");
                Rename(frame, "shearMix", "mixShearY");
            }
            ConvertDependentMixCurves(frames, true);
        }
    }

    private static void ConvertIkTimelines(JsonObject animation)
    {
        if (animation["ik"] is not JsonObject timelines)
            return;
        foreach (var (_, timelineNode) in timelines)
        {
            if (timelineNode is not JsonArray frames)
                throw new ConversionException("IK constraint timelines must be arrays.");
            ConvertCurves(frames, ["mix", "softness"], [1, 0]);
        }
    }

    private static void ConvertPathTimelines(JsonObject animation)
    {
        if (animation["path"] is not JsonObject constraints)
            return;
        foreach (var (_, constraintNode) in constraints)
        {
            if (constraintNode is not JsonObject timelines)
                throw new ConversionException("Path constraint entries must be objects.");
            foreach (var (timelineName, timelineNode) in timelines)
            {
                if (timelineNode is not JsonArray frames)
                    throw new ConversionException($"Path timeline '{timelineName}' must be an array.");
                switch (timelineName)
                {
                    case "position":
                    case "spacing":
                        foreach (var node in frames)
                        {
                            var frame = node?.AsObject() ?? throw new ConversionException("Path frames must be objects.");
                            Rename(frame, timelineName, "value");
                        }
                        ConvertCurves(frames, ["value"], [0]);
                        break;
                    case "mix":
                        foreach (var node in frames)
                        {
                            var frame = node?.AsObject() ?? throw new ConversionException("Path mix frames must be objects.");
                            Rename(frame, "rotateMix", "mixRotate");
                            Rename(frame, "translateMix", "mixX");
                        }
                        ConvertDependentMixCurves(frames, false);
                        break;
                    default:
                        throw new ConversionException($"Unsupported Spine 3.8 path timeline: {timelineName}");
                }
            }
        }
    }

    private static void ConvertDependentMixCurves(JsonArray frames, bool transform)
    {
        for (var index = 0; index + 1 < frames.Count; index++)
        {
            var frame = frames[index]?.AsObject() ?? throw new ConversionException("Mix frames must be objects.");
            if (!IsBezier(frame))
                continue;
            var next = frames[index + 1]?.AsObject() ?? throw new ConversionException("Mix frames must be objects.");
            var currentX = Number(frame, "mixX", 1);
            var nextX = Number(next, "mixX", 1);
            var current = new List<double> { Number(frame, "mixRotate", 1), currentX, Number(frame, "mixY", currentX) };
            var following = new List<double> { Number(next, "mixRotate", 1), nextX, Number(next, "mixY", nextX) };
            if (transform)
            {
                var currentScaleX = Number(frame, "mixScaleX", 1);
                var nextScaleX = Number(next, "mixScaleX", 1);
                current.Add(Number(frame, "mixScaleX", 1));
                current.Add(Number(frame, "mixScaleY", currentScaleX));
                current.Add(Number(frame, "mixShearY", 1));
                following.Add(Number(next, "mixScaleX", 1));
                following.Add(Number(next, "mixScaleY", nextScaleX));
                following.Add(Number(next, "mixShearY", 1));
            }
            SetAbsoluteCurve(frame, next, current, following);
        }
    }

    private static void ConvertDeformTimelines(JsonObject animation)
    {
        if (animation["deform"] is not JsonObject skins)
            return;
        var attachments = new JsonObject();
        foreach (var (skinName, skinNode) in skins)
        {
            var sourceSlots = skinNode?.AsObject() ?? throw new ConversionException("Deform skin entries must be objects.");
            var targetSlots = new JsonObject();
            foreach (var (slotName, slotNode) in sourceSlots)
            {
                var sourceAttachments = slotNode?.AsObject() ?? throw new ConversionException("Deform slot entries must be objects.");
                var targetAttachments = new JsonObject();
                foreach (var (attachmentName, framesNode) in sourceAttachments)
                {
                    var frames = framesNode?.AsArray() ?? throw new ConversionException("Deform timelines must be arrays.");
                    ConvertUnknownWidthCurves(frames);
                    targetAttachments[attachmentName] = new JsonObject { ["deform"] = frames.DeepClone() };
                }
                targetSlots[slotName] = targetAttachments;
            }
            attachments[skinName] = targetSlots;
        }
        animation.Remove("deform");
        animation["attachments"] = attachments;
    }

    private static void ConvertUnknownWidthCurves(JsonArray frames)
    {
        for (var index = 0; index + 1 < frames.Count; index++)
        {
            var frame = frames[index]?.AsObject() ?? throw new ConversionException("Deform frames must be objects.");
            if (!IsBezier(frame))
                continue;
            // Deform curves share one normalized bezier across all vertex values. Spine 4.2
            // accepts a four-value absolute curve for this timeline.
            var next = frames[index + 1]?.AsObject() ?? throw new ConversionException("Deform frames must be objects.");
            SetAbsoluteCurve(frame, next, [0], [1]);
        }
    }

    private static void ConvertCurves(JsonArray frames, string[] keys, double[] defaults)
    {
        for (var index = 0; index + 1 < frames.Count; index++)
        {
            var frame = frames[index]?.AsObject() ?? throw new ConversionException("Timeline frames must be objects.");
            if (!IsBezier(frame))
                continue;
            var next = frames[index + 1]?.AsObject() ?? throw new ConversionException("Timeline frames must be objects.");
            SetAbsoluteCurve(
                frame,
                next,
                keys.Select((key, i) => Number(frame, key, defaults[i])).ToArray(),
                keys.Select((key, i) => Number(next, key, defaults[i])).ToArray());
        }
    }

    private static bool IsBezier(JsonObject frame) => frame["curve"] is JsonValue value
        && value.TryGetValue<double>(out _);

    private static void SetAbsoluteCurve(
        JsonObject frame,
        JsonObject next,
        IReadOnlyList<double> currentValues,
        IReadOnlyList<double> nextValues)
    {
        var c1 = Number(frame, "curve", 0);
        var c2 = Number(frame, "c2", 0);
        var c3 = Number(frame, "c3", 1);
        var c4 = Number(frame, "c4", 1);
        var time = Number(frame, "time", 0);
        var nextTime = Number(next, "time", 0);
        var result = new JsonArray();
        for (var i = 0; i < currentValues.Count; i++)
        {
            var value = currentValues[i];
            var delta = nextValues[i] - value;
            result.Add(time + c1 * (nextTime - time));
            result.Add(value + c2 * delta);
            result.Add(time + c3 * (nextTime - time));
            result.Add(value + c4 * delta);
        }
        frame["curve"] = result;
        frame.Remove("c2");
        frame.Remove("c3");
        frame.Remove("c4");
    }

    private static void Rename(JsonObject value, string source, string target)
    {
        if (value[source] is null)
            return;
        value[target] = value[source]!.DeepClone();
        value.Remove(source);
    }

    private static JsonObject Object(JsonObject parent, string name, string error) =>
        parent[name] as JsonObject ?? throw new ConversionException(error);

    private static string Text(JsonObject value, string name) =>
        value[name]?.GetValue<string>() ?? throw new ConversionException($"Missing required string '{name}'.");

    private static double Number(JsonObject value, string name, double fallback) =>
        value[name] is JsonValue node && node.TryGetValue<double>(out var result) ? result : fallback;

    private static double[] Zeroes(int count) => Enumerable.Repeat(0d, count).ToArray();
    private static double[] Ones(int count) => Enumerable.Repeat(1d, count).ToArray();
}
