using System.Text.Json.Nodes;

namespace SpineConverter.Core;

public sealed partial class Spine38BinaryJsonReader
{
    private void ReadSkins(JsonObject root)
    {
        var output = new JsonArray();
        var defaultSlots = Count("default skin slots");
        if (defaultSlots > 0)
        {
            _skins.Add("default");
            output.Add(new JsonObject
            {
                ["name"] = "default",
                ["attachments"] = ReadSkinAttachments(defaultSlots),
            });
        }

        var count = Count("skins");
        for (var i = 0; i < count; i++)
        {
            var name = RefString() ?? throw new ConversionException($"Skin {i} has no name.");
            _skins.Add(name);
            var skin = new JsonObject { ["name"] = name };
            SetNameList(skin, "bones", _bones, "skin bones");
            SetNameList(skin, "ik", _ik, "skin IK constraints");
            SetNameList(skin, "transform", _transform, "skin transform constraints");
            SetNameList(skin, "path", _paths, "skin path constraints");
            var slotCount = Count("skin slots");
            skin["attachments"] = ReadSkinAttachments(slotCount);
            output.Add(skin);
        }
        if (output.Count > 0) root["skins"] = output;
    }

    private void SetNameList(JsonObject output, string key, List<string> names, string label)
    {
        var count = Count(label);
        if (count == 0) return;
        var values = new JsonArray();
        for (var i = 0; i < count; i++) values.Add(Name(names, _input.ReadVarInt(true), label));
        output[key] = values;
    }

    private JsonObject ReadSkinAttachments(int slotCount)
    {
        var slots = new JsonObject();
        for (var slot = 0; slot < slotCount; slot++)
        {
            var slotName = Name(_slots, _input.ReadVarInt(true), "skin slot");
            var attachments = new JsonObject();
            var count = Count("slot attachments");
            for (var attachment = 0; attachment < count; attachment++)
            {
                var placeholder = RefString() ?? throw new ConversionException("Attachment placeholder is null.");
                attachments[placeholder] = ReadAttachment(placeholder);
            }
            slots[slotName] = attachments;
        }
        return slots;
    }

    private JsonObject ReadAttachment(string placeholder)
    {
        var actualName = RefString() ?? placeholder;
        var type = _input.ReadByte();
        var output = new JsonObject();
        if (actualName != placeholder) output["name"] = actualName;
        switch (type)
        {
            case 0:
                ReadRegion(output, actualName);
                break;
            case 1:
                output["type"] = "boundingbox";
                ReadVertexAttachment(output, Count("bounding box vertices"));
                if (_nonessential) output["color"] = Rgba(_input.ReadInt32());
                break;
            case 2:
                ReadMesh(output, actualName);
                break;
            case 3:
                ReadLinkedMesh(output, actualName);
                break;
            case 4:
                ReadPathAttachment(output);
                break;
            case 5:
                ReadPointAttachment(output);
                break;
            case 6:
                ReadClippingAttachment(output);
                break;
            default:
                throw new ConversionException($"Unknown attachment type {type}.");
        }
        return output;
    }

    private void ReadRegion(JsonObject output, string actualName)
    {
        var path = RefString() ?? actualName;
        if (path != actualName) output["path"] = path;
        Set(output, "rotation", _input.ReadSingle(), 0);
        Set(output, "x", _input.ReadSingle(), 0);
        Set(output, "y", _input.ReadSingle(), 0);
        Set(output, "scaleX", _input.ReadSingle(), 1);
        Set(output, "scaleY", _input.ReadSingle(), 1);
        output["width"] = _input.ReadSingle();
        output["height"] = _input.ReadSingle();
        var color = _input.ReadInt32();
        if (unchecked((uint)color) != 0xffffffff) output["color"] = Rgba(color);
    }

    private void ReadMesh(JsonObject output, string actualName)
    {
        output["type"] = "mesh";
        var path = RefString() ?? actualName;
        if (path != actualName) output["path"] = path;
        var color = _input.ReadInt32();
        if (unchecked((uint)color) != 0xffffffff) output["color"] = Rgba(color);
        var vertexCount = Count("mesh vertices");
        output["uvs"] = ReadFloatArray(vertexCount * 2);
        output["triangles"] = ReadShortArray("mesh triangles");
        output["vertices"] = ReadVertices(vertexCount);
        output["hull"] = _input.ReadVarInt(true);
        if (_nonessential)
        {
            output["edges"] = ReadShortArray("mesh edges");
            output["width"] = _input.ReadSingle();
            output["height"] = _input.ReadSingle();
        }
    }

    private void ReadLinkedMesh(JsonObject output, string actualName)
    {
        output["type"] = "linkedmesh";
        var path = RefString() ?? actualName;
        if (path != actualName) output["path"] = path;
        var color = _input.ReadInt32();
        if (unchecked((uint)color) != 0xffffffff) output["color"] = Rgba(color);
        var skin = RefString();
        if (skin is not null) output["skin"] = skin;
        output["parent"] = RefString() ?? throw new ConversionException("Linked mesh parent is null.");
        if (!_input.ReadBoolean()) output["deform"] = false;
        if (_nonessential)
        {
            output["width"] = _input.ReadSingle();
            output["height"] = _input.ReadSingle();
        }
    }

    private void ReadPathAttachment(JsonObject output)
    {
        output["type"] = "path";
        if (_input.ReadBoolean()) output["closed"] = true;
        if (!_input.ReadBoolean()) output["constantSpeed"] = false;
        var vertexCount = Count("path vertices");
        output["vertexCount"] = vertexCount;
        output["vertices"] = ReadVertices(vertexCount);
        output["lengths"] = ReadFloatArray(vertexCount / 3);
        if (_nonessential) output["color"] = Rgba(_input.ReadInt32());
    }

    private void ReadPointAttachment(JsonObject output)
    {
        output["type"] = "point";
        Set(output, "rotation", _input.ReadSingle(), 0);
        Set(output, "x", _input.ReadSingle(), 0);
        Set(output, "y", _input.ReadSingle(), 0);
        if (_nonessential) output["color"] = Rgba(_input.ReadInt32());
    }

    private void ReadClippingAttachment(JsonObject output)
    {
        output["type"] = "clipping";
        output["end"] = Name(_slots, _input.ReadVarInt(true), "clipping end slot");
        var vertexCount = Count("clipping vertices");
        output["vertexCount"] = vertexCount;
        output["vertices"] = ReadVertices(vertexCount);
        if (_nonessential) output["color"] = Rgba(_input.ReadInt32());
    }

    private void ReadVertexAttachment(JsonObject output, int vertexCount)
    {
        output["vertexCount"] = vertexCount;
        output["vertices"] = ReadVertices(vertexCount);
    }

    private JsonArray ReadVertices(int vertexCount)
    {
        if (!_input.ReadBoolean()) return ReadFloatArray(vertexCount * 2);
        var output = new JsonArray();
        for (var vertex = 0; vertex < vertexCount; vertex++)
        {
            var boneCount = Count("vertex influences");
            output.Add(boneCount);
            for (var influence = 0; influence < boneCount; influence++)
            {
                var bone = _input.ReadVarInt(true);
                _ = Name(_bones, bone, "vertex bone");
                output.Add(bone);
                output.Add(_input.ReadSingle());
                output.Add(_input.ReadSingle());
                output.Add(_input.ReadSingle());
            }
        }
        return output;
    }

    private JsonArray ReadFloatArray(int count)
    {
        var output = new JsonArray();
        for (var i = 0; i < count; i++) output.Add(_input.ReadSingle());
        return output;
    }

    private JsonArray ReadShortArray(string label)
    {
        var output = new JsonArray();
        var count = Count(label);
        for (var i = 0; i < count; i++) output.Add(_input.ReadUInt16());
        return output;
    }
}
