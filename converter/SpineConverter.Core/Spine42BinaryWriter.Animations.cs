using System.Globalization;
using System.Text.Json.Nodes;

namespace SpineConverter.Core;

public sealed partial class Spine42BinaryWriter
{
    private void WriteEvents(JsonObject root)
    {
        var events = root["events"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(events.Count, true);
        foreach (var (name, node) in events)
        {
            var value = node!.AsObject();
            var intValue = Int(value, "int");
            var floatValue = Float(value, "float");
            var stringValue = TextOrNull(value, "string");
            var audio = TextOrNull(value, "audio");
            _events.Add(new EventInfo(name, intValue, floatValue, stringValue, audio is not null));
            _output.WriteString(name);
            _output.WriteVarInt(intValue, false);
            _output.WriteSingle(floatValue);
            _output.WriteString(stringValue);
            _output.WriteString(audio);
            if (audio is not null)
            {
                _output.WriteSingle(Float(value, "volume", 1));
                _output.WriteSingle(Float(value, "balance"));
            }
        }
    }

    private void WriteAnimations(JsonObject root)
    {
        var animations = root["animations"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(animations.Count, true);
        foreach (var (name, node) in animations)
        {
            _output.WriteString(name);
            WriteAnimation(node!.AsObject());
        }
    }

    private void WriteAnimation(JsonObject animation)
    {
        _output.WriteVarInt(CountTimelines(animation), true);
        WriteSlotTimelines(animation);
        WriteBoneTimelines(animation);
        WriteIkTimelines(animation);
        WriteTransformTimelines(animation);
        WritePathTimelines(animation);
        _output.WriteVarInt(0, true); // Physics timelines.
        WriteAttachmentTimelines(animation);
        WriteDrawOrder(animation);
        WriteEventTimeline(animation);
    }

    private static int CountTimelines(JsonObject animation)
    {
        var count = 0;
        count += CountNestedTimelines(animation["slots"] as JsonObject);
        count += CountNestedTimelines(animation["bones"] as JsonObject);
        count += (animation["ik"] as JsonObject)?.Count ?? 0;
        count += (animation["transform"] as JsonObject)?.Count ?? 0;
        count += CountNestedTimelines(animation["path"] as JsonObject);
        if (animation["attachments"] is JsonObject skins)
            foreach (var (_, skinNode) in skins)
            foreach (var (_, slotNode) in skinNode!.AsObject())
            foreach (var (_, attachmentNode) in slotNode!.AsObject()) count += attachmentNode!.AsObject().Count;
        if (animation["drawOrder"] is JsonArray { Count: > 0 }) count++;
        if (animation["events"] is JsonArray { Count: > 0 }) count++;
        return count;
    }

    private static int CountNestedTimelines(JsonObject? entries)
    {
        if (entries is null) return 0;
        var count = 0;
        foreach (var (_, node) in entries) count += node!.AsObject().Count;
        return count;
    }

    private void WriteSlotTimelines(JsonObject animation)
    {
        var slots = animation["slots"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(slots.Count, true);
        foreach (var (slotName, node) in slots)
        {
            _output.WriteVarInt(Index(_slots, slotName, "animated slot"), true);
            var timelines = node!.AsObject();
            _output.WriteVarInt(timelines.Count, true);
            foreach (var (timelineName, framesNode) in timelines)
            {
                var frames = framesNode!.AsArray();
                switch (timelineName)
                {
                    case "attachment":
                        _output.WriteByte(0);
                        _output.WriteVarInt(frames.Count, true);
                        foreach (var frameNode in frames)
                        {
                            var frame = frameNode!.AsObject();
                            _output.WriteSingle(Float(frame, "time"));
                            WriteRef(TextOrNull(frame, "name"));
                        }
                        break;
                    case "rgba":
                        WriteColorTimeline(1, frames, ["color"], [true]);
                        break;
                    case "rgba2":
                        WriteColorTimeline(3, frames, ["light", "dark"], [true, false]);
                        break;
                    default:
                        throw new ConversionException($"Unsupported 4.2 slot timeline '{timelineName}'.");
                }
            }
        }
    }

    private void WriteColorTimeline(int type, JsonArray frames, string[] keys, bool[] alpha)
    {
        var channels = alpha.Sum(value => value ? 4 : 3);
        _output.WriteByte(type);
        _output.WriteVarInt(frames.Count, true);
        _output.WriteVarInt(BezierCount(frames, channels), true);
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index]!.AsObject();
            _output.WriteSingle(Float(frame, "time"));
            for (var key = 0; key < keys.Length; key++)
                foreach (var component in ColorBytes(Text(frame, keys[key]), alpha[key])) _output.WriteByte(component);
            if (index > 0) WriteCurve(frames[index - 1]!.AsObject(), channels);
        }
    }

    private void WriteBoneTimelines(JsonObject animation)
    {
        var bones = animation["bones"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(bones.Count, true);
        foreach (var (boneName, node) in bones)
        {
            _output.WriteVarInt(Index(_bones, boneName, "animated bone"), true);
            var timelines = node!.AsObject();
            _output.WriteVarInt(timelines.Count, true);
            foreach (var (timelineName, framesNode) in timelines)
            {
                var frames = framesNode!.AsArray();
                var (type, keys, defaults) = timelineName switch
                {
                    "rotate" => (0, new[] { "value" }, new[] { 0f }),
                    "translate" => (1, new[] { "x", "y" }, new[] { 0f, 0f }),
                    "scale" => (4, new[] { "x", "y" }, new[] { 1f, 1f }),
                    "shear" => (7, new[] { "x", "y" }, new[] { 0f, 0f }),
                    _ => throw new ConversionException($"Unsupported 4.2 bone timeline '{timelineName}'."),
                };
                WriteValueTimeline(type, frames, keys, defaults);
            }
        }
    }

    private void WriteValueTimeline(int type, JsonArray frames, string[] keys, float[] defaults)
    {
        _output.WriteByte(type);
        _output.WriteVarInt(frames.Count, true);
        _output.WriteVarInt(BezierCount(frames, keys.Length), true);
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index]!.AsObject();
            _output.WriteSingle(Float(frame, "time"));
            for (var key = 0; key < keys.Length; key++) _output.WriteSingle(Float(frame, keys[key], defaults[key]));
            if (index > 0) WriteCurve(frames[index - 1]!.AsObject(), keys.Length);
        }
    }

    private void WriteIkTimelines(JsonObject animation)
    {
        var timelines = animation["ik"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(timelines.Count, true);
        foreach (var (name, node) in timelines)
        {
            var frames = node!.AsArray();
            _output.WriteVarInt(Index(_ik, name, "IK timeline"), true);
            _output.WriteVarInt(frames.Count, true);
            _output.WriteVarInt(BezierCount(frames, 2), true);
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index]!.AsObject();
                var mix = Float(frame, "mix", 1);
                var softness = Float(frame, "softness");
                var flags = 0;
                if (mix != 0) { flags |= 1; if (mix != 1) flags |= 2; }
                if (softness != 0) flags |= 4;
                if (Bool(frame, "bendPositive", true)) flags |= 8;
                if (Bool(frame, "compress")) flags |= 16;
                if (Bool(frame, "stretch")) flags |= 32;
                if (index > 0)
                {
                    var curve = frames[index - 1]!["curve"];
                    if (curve is JsonValue) flags |= 64;
                    else if (curve is JsonArray) flags |= 128;
                }
                _output.WriteByte(flags);
                _output.WriteSingle(Float(frame, "time"));
                if ((flags & 2) != 0) _output.WriteSingle(mix);
                if ((flags & 4) != 0) _output.WriteSingle(softness);
                if ((flags & 128) != 0) WriteBezierValues(frames[index - 1]!.AsObject(), 2);
            }
        }
    }

    private void WriteTransformTimelines(JsonObject animation)
    {
        var timelines = animation["transform"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(timelines.Count, true);
        foreach (var (name, node) in timelines)
        {
            var frames = node!.AsArray();
            _output.WriteVarInt(Index(_transform, name, "transform timeline"), true);
            _output.WriteVarInt(frames.Count, true);
            _output.WriteVarInt(BezierCount(frames, 6), true);
            string[] keys = ["mixRotate", "mixX", "mixY", "mixScaleX", "mixScaleY", "mixShearY"];
            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index]!.AsObject();
                _output.WriteSingle(Float(frame, "time"));
                var mixX = Float(frame, "mixX", 1);
                var mixScaleX = Float(frame, "mixScaleX", 1);
                float[] values = [Float(frame, keys[0], 1), mixX, Float(frame, keys[2], mixX), mixScaleX,
                    Float(frame, keys[4], mixScaleX), Float(frame, keys[5], 1)];
                foreach (var value in values) _output.WriteSingle(value);
                if (index > 0) WriteCurve(frames[index - 1]!.AsObject(), 6);
            }
        }
    }

    private void WritePathTimelines(JsonObject animation)
    {
        var paths = animation["path"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(paths.Count, true);
        foreach (var (pathName, node) in paths)
        {
            _output.WriteVarInt(Index(_paths, pathName, "path timeline"), true);
            var timelines = node!.AsObject();
            _output.WriteVarInt(timelines.Count, true);
            foreach (var (timelineName, framesNode) in timelines)
            {
                var frames = framesNode!.AsArray();
                if (timelineName == "position") WriteValueTimeline(0, frames, ["value"], [0]);
                else if (timelineName == "spacing") WriteValueTimeline(1, frames, ["value"], [0]);
                else if (timelineName == "mix")
                {
                    _output.WriteByte(2);
                    _output.WriteVarInt(frames.Count, true);
                    _output.WriteVarInt(BezierCount(frames, 3), true);
                    for (var index = 0; index < frames.Count; index++)
                    {
                        var frame = frames[index]!.AsObject();
                        var mixX = Float(frame, "mixX", 1);
                        _output.WriteSingle(Float(frame, "time"));
                        _output.WriteSingle(Float(frame, "mixRotate", 1));
                        _output.WriteSingle(mixX);
                        _output.WriteSingle(Float(frame, "mixY", mixX));
                        if (index > 0) WriteCurve(frames[index - 1]!.AsObject(), 3);
                    }
                }
                else throw new ConversionException($"Unsupported 4.2 path timeline '{timelineName}'.");
            }
        }
    }

    private void WriteAttachmentTimelines(JsonObject animation)
    {
        var skins = animation["attachments"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(skins.Count, true);
        foreach (var (skinName, skinNode) in skins)
        {
            _output.WriteVarInt(Index(_skins, skinName, "attachment timeline skin"), true);
            var slots = skinNode!.AsObject();
            _output.WriteVarInt(slots.Count, true);
            foreach (var (slotName, slotNode) in slots)
            {
                _output.WriteVarInt(Index(_slots, slotName, "attachment timeline slot"), true);
                var attachments = slotNode!.AsObject();
                _output.WriteVarInt(attachments.Count, true);
                foreach (var (attachmentName, attachmentNode) in attachments)
                {
                    WriteRef(attachmentName);
                    var timelines = attachmentNode!.AsObject();
                    if (timelines.Count != 1 || !timelines.ContainsKey("deform"))
                        throw new ConversionException(
                            $"Attachment '{attachmentName}' must contain exactly one deform timeline.");
                    foreach (var (timelineName, framesNode) in timelines)
                    {
                        if (timelineName != "deform")
                            throw new ConversionException($"Unsupported attachment timeline '{timelineName}'.");
                        var frames = framesNode!.AsArray();
                        _output.WriteByte(0);
                        _output.WriteVarInt(frames.Count, true);
                        _output.WriteVarInt(BezierCount(frames, 1), true);
                        if (frames.Count == 0) continue;
                        _output.WriteSingle(Float(frames[0]!.AsObject(), "time"));
                        for (var index = 0; index < frames.Count; index++)
                        {
                            var frame = frames[index]!.AsObject();
                            var vertices = frame["vertices"] as JsonArray;
                            _output.WriteVarInt(vertices?.Count ?? 0, true);
                            if (vertices is not null)
                            {
                                _output.WriteVarInt(Int(frame, "offset"), true);
                                WriteFloatArray(vertices);
                            }
                            if (index + 1 < frames.Count)
                            {
                                _output.WriteSingle(Float(frames[index + 1]!.AsObject(), "time"));
                                WriteCurve(frame, 1);
                            }
                        }
                    }
                }
            }
        }
    }

    private void WriteDrawOrder(JsonObject animation)
    {
        var frames = animation["drawOrder"] as JsonArray ?? [];
        _output.WriteVarInt(frames.Count, true);
        foreach (var node in frames)
        {
            var frame = node!.AsObject();
            _output.WriteSingle(Float(frame, "time"));
            var offsets = frame["offsets"] as JsonArray ?? [];
            _output.WriteVarInt(offsets.Count, true);
            foreach (var offsetNode in offsets)
            {
                var offset = offsetNode!.AsObject();
                _output.WriteVarInt(Index(_slots, Text(offset, "slot"), "draw order slot"), true);
                _output.WriteVarInt(Int(offset, "offset"), true);
            }
        }
    }

    private void WriteEventTimeline(JsonObject animation)
    {
        var frames = animation["events"] as JsonArray ?? [];
        _output.WriteVarInt(frames.Count, true);
        foreach (var node in frames)
        {
            var frame = node!.AsObject();
            _output.WriteSingle(Float(frame, "time"));
            var eventIndex = Index(_events.Select(value => value.Name).ToArray(), Text(frame, "name"), "event");
            var setup = _events[eventIndex];
            _output.WriteVarInt(eventIndex, true);
            _output.WriteVarInt(Int(frame, "int", setup.Int), false);
            _output.WriteSingle(Float(frame, "float", setup.Float));
            _output.WriteString(TextOrNull(frame, "string"));
            if (setup.HasAudio)
            {
                _output.WriteSingle(Float(frame, "volume", 1));
                _output.WriteSingle(Float(frame, "balance"));
            }
        }
    }

    private static int BezierCount(JsonArray frames, int channels)
    {
        var count = 0;
        for (var i = 0; i + 1 < frames.Count; i++)
            if (frames[i]?["curve"] is JsonArray) count += channels;
        return count;
    }

    private void WriteCurve(JsonObject frame, int channels)
    {
        if (frame["curve"] is JsonValue value && value.TryGetValue<string>(out var text) && text == "stepped")
        {
            _output.WriteByte(1);
            return;
        }
        if (frame["curve"] is JsonArray)
        {
            _output.WriteByte(2);
            WriteBezierValues(frame, channels);
            return;
        }
        _output.WriteByte(0);
    }

    private void WriteBezierValues(JsonObject frame, int channels)
    {
        var curve = frame["curve"] as JsonArray
            ?? throw new ConversionException("Bezier frame is missing its curve array.");
        if (curve.Count != channels * 4)
            throw new ConversionException($"Bezier curve has {curve.Count} values, expected {channels * 4}.");
        WriteFloatArray(curve);
    }

    private static IEnumerable<byte> ColorBytes(string value, bool alpha)
    {
        var expected = alpha ? 8 : 6;
        if (value.Length != expected) throw new ConversionException($"Invalid color '{value}'.");
        for (var index = 0; index < expected; index += 2)
            yield return byte.Parse(value.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
