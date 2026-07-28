using System.Text.Json.Nodes;

namespace SpineConverter.Core;

public sealed partial class Spine42BinaryWriter
{
    private void WriteSkins(JsonObject root)
    {
        var skins = root["skins"] as JsonArray ?? [];
        foreach (var node in skins) _skins.Add(Text(node!.AsObject(), "name"));
        var defaultIndex = _skins.IndexOf("default");
        if (defaultIndex >= 0)
            WriteSkinAttachments(skins[defaultIndex]!.AsObject());
        else
            _output.WriteVarInt(0, true);

        _output.WriteVarInt(skins.Count - (defaultIndex >= 0 ? 1 : 0), true);
        for (var index = 0; index < skins.Count; index++)
        {
            if (index == defaultIndex) continue;
            var skin = skins[index]!.AsObject();
            _output.WriteString(Text(skin, "name"));
            WriteOptionalNameIndices(skin, "bones", _bones, "skin bones");
            WriteOptionalNameIndices(skin, "ik", _ik, "skin IK constraints");
            WriteOptionalNameIndices(skin, "transform", _transform, "skin transform constraints");
            WriteOptionalNameIndices(skin, "path", _paths, "skin path constraints");
            _output.WriteVarInt(0, true); // Physics constraints.
            WriteSkinAttachments(skin);
        }
    }

    private void WriteOptionalNameIndices(JsonObject parent, string key, List<string> names, string label)
    {
        var values = parent[key] as JsonArray ?? [];
        WriteNameIndices(values, names, label);
    }

    private void WriteSkinAttachments(JsonObject skin)
    {
        var attachments = skin["attachments"] as JsonObject ?? new JsonObject();
        _output.WriteVarInt(attachments.Count, true);
        foreach (var (slotName, slotNode) in attachments)
        {
            _output.WriteVarInt(Index(_slots, slotName, "skin slot"), true);
            var slotAttachments = slotNode!.AsObject();
            _output.WriteVarInt(slotAttachments.Count, true);
            foreach (var (placeholder, attachmentNode) in slotAttachments)
            {
                WriteRef(placeholder);
                WriteAttachment(placeholder, attachmentNode!.AsObject());
            }
        }
    }

    private void WriteAttachment(string placeholder, JsonObject attachment)
    {
        var typeName = TextOrNull(attachment, "type") ?? "region";
        string[] typeNames = ["region", "boundingbox", "mesh", "linkedmesh", "path", "point", "clipping"];
        var type = Index(typeNames, typeName, "attachment type");
        var actualName = TextOrNull(attachment, "name") ?? placeholder;
        var flags = type;
        if (actualName != placeholder) flags |= 8;
        switch (type)
        {
            case 0: WriteRegionAttachment(flags, actualName, attachment); break;
            case 1: WriteBoundingBoxAttachment(flags, actualName, attachment); break;
            case 2: WriteMeshAttachment(flags, actualName, attachment); break;
            case 3: WriteLinkedMeshAttachment(flags, actualName, attachment); break;
            case 4: WritePathAttachment(flags, actualName, attachment); break;
            case 5: WritePointAttachment(flags, actualName, attachment); break;
            case 6: WriteClippingAttachment(flags, actualName, attachment); break;
        }
    }

    private void WriteRegionAttachment(int flags, string name, JsonObject value)
    {
        var path = TextOrNull(value, "path") ?? name;
        var color = TextOrNull(value, "color") ?? "ffffffff";
        if (path != name) flags |= 16;
        if (color != "ffffffff") flags |= 32;
        if (Float(value, "rotation") != 0) flags |= 128;
        _output.WriteByte(flags);
        if ((flags & 8) != 0) WriteRef(name);
        if ((flags & 16) != 0) WriteRef(path);
        if ((flags & 32) != 0) _output.WriteInt32(Color(color, true));
        if ((flags & 128) != 0) _output.WriteSingle(Float(value, "rotation"));
        _output.WriteSingle(Float(value, "x"));
        _output.WriteSingle(Float(value, "y"));
        _output.WriteSingle(Float(value, "scaleX", 1));
        _output.WriteSingle(Float(value, "scaleY", 1));
        _output.WriteSingle(Float(value, "width"));
        _output.WriteSingle(Float(value, "height"));
    }

    private void WriteBoundingBoxAttachment(int flags, string name, JsonObject value)
    {
        var vertexCount = Int(value, "vertexCount");
        var vertices = Array(value, "vertices");
        var weighted = IsWeighted(vertices, vertexCount);
        if (weighted) flags |= 16;
        _output.WriteByte(flags);
        if ((flags & 8) != 0) WriteRef(name);
        WriteVertices(vertices, vertexCount, weighted);
    }

    private void WriteMeshAttachment(int flags, string name, JsonObject value)
    {
        var path = TextOrNull(value, "path") ?? name;
        var color = TextOrNull(value, "color") ?? "ffffffff";
        var uvs = Array(value, "uvs");
        if ((uvs.Count & 1) != 0) throw new ConversionException("Mesh UV count must be even.");
        var vertexCount = uvs.Count / 2;
        var vertices = Array(value, "vertices");
        var weighted = IsWeighted(vertices, vertexCount);
        if (path != name) flags |= 16;
        if (color != "ffffffff") flags |= 32;
        if (weighted) flags |= 128;
        _output.WriteByte(flags);
        if ((flags & 8) != 0) WriteRef(name);
        if ((flags & 16) != 0) WriteRef(path);
        if ((flags & 32) != 0) _output.WriteInt32(Color(color, true));
        var triangles = Array(value, "triangles");
        if (triangles.Count % 3 != 0)
            throw new ConversionException("Mesh triangle index count must be divisible by three.");
        // Spine 4.2 binary omits the triangle count and derives it from this field.
        // Recalculate it from the actual 3.8 triangle data so no indices are lost.
        var binaryHull = vertexCount * 2 - 2 - triangles.Count / 3;
        if (binaryHull < 0)
            throw new ConversionException("Mesh triangle count cannot be represented in Spine 4.2 binary.");
        _output.WriteVarInt(binaryHull, true);
        WriteVertices(vertices, vertexCount, weighted);
        WriteFloatArray(uvs);
        foreach (var triangle in triangles) _output.WriteVarInt(triangle!.GetValue<int>(), true);
    }

    private void WriteLinkedMeshAttachment(int flags, string name, JsonObject value)
    {
        var path = TextOrNull(value, "path") ?? name;
        var color = TextOrNull(value, "color") ?? "ffffffff";
        if (path != name) flags |= 16;
        if (color != "ffffffff") flags |= 32;
        if (Bool(value, "deform", true)) flags |= 128;
        _output.WriteByte(flags);
        if ((flags & 8) != 0) WriteRef(name);
        if ((flags & 16) != 0) WriteRef(path);
        if ((flags & 32) != 0) _output.WriteInt32(Color(color, true));
        var skinName = TextOrNull(value, "skin") ?? "default";
        _output.WriteVarInt(Index(_skins, skinName, "linked mesh skin"), true);
        WriteRef(Text(value, "parent"));
    }

    private void WritePathAttachment(int flags, string name, JsonObject value)
    {
        var vertexCount = Int(value, "vertexCount");
        var vertices = Array(value, "vertices");
        var weighted = IsWeighted(vertices, vertexCount);
        if (Bool(value, "closed")) flags |= 16;
        if (Bool(value, "constantSpeed", true)) flags |= 32;
        if (weighted) flags |= 64;
        _output.WriteByte(flags);
        if ((flags & 8) != 0) WriteRef(name);
        WriteVertices(vertices, vertexCount, weighted);
        WriteFloatArray(Array(value, "lengths"));
    }

    private void WritePointAttachment(int flags, string name, JsonObject value)
    {
        _output.WriteByte(flags);
        if ((flags & 8) != 0) WriteRef(name);
        _output.WriteSingle(Float(value, "rotation"));
        _output.WriteSingle(Float(value, "x"));
        _output.WriteSingle(Float(value, "y"));
    }

    private void WriteClippingAttachment(int flags, string name, JsonObject value)
    {
        var vertexCount = Int(value, "vertexCount");
        var vertices = Array(value, "vertices");
        var weighted = IsWeighted(vertices, vertexCount);
        if (weighted) flags |= 16;
        _output.WriteByte(flags);
        if ((flags & 8) != 0) WriteRef(name);
        _output.WriteVarInt(Index(_slots, Text(value, "end"), "clipping end slot"), true);
        WriteVertices(vertices, vertexCount, weighted);
    }

    private static bool IsWeighted(JsonArray vertices, int vertexCount) => vertices.Count != vertexCount * 2;

    private void WriteVertices(JsonArray vertices, int vertexCount, bool weighted)
    {
        _output.WriteVarInt(vertexCount, true);
        if (!weighted)
        {
            if (vertices.Count != vertexCount * 2)
                throw new ConversionException("Unweighted vertex array length does not match vertex count.");
            WriteFloatArray(vertices);
            return;
        }
        var cursor = 0;
        for (var vertex = 0; vertex < vertexCount; vertex++)
        {
            if (cursor >= vertices.Count) throw new ConversionException("Weighted vertex array ended early.");
            var boneCount = vertices[cursor++]!.GetValue<int>();
            _output.WriteVarInt(boneCount, true);
            for (var influence = 0; influence < boneCount; influence++)
            {
                if (cursor + 3 >= vertices.Count) throw new ConversionException("Weighted vertex influence ended early.");
                _output.WriteVarInt(vertices[cursor++]!.GetValue<int>(), true);
                _output.WriteSingle(vertices[cursor++]!.GetValue<float>());
                _output.WriteSingle(vertices[cursor++]!.GetValue<float>());
                _output.WriteSingle(vertices[cursor++]!.GetValue<float>());
            }
        }
        if (cursor != vertices.Count) throw new ConversionException("Weighted vertex array has trailing values.");
    }

    private void WriteFloatArray(JsonArray values)
    {
        foreach (var value in values) _output.WriteSingle(value!.GetValue<float>());
    }
}
