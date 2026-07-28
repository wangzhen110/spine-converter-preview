using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using SpineConverter.Core;

Console.OutputEncoding = Encoding.UTF8;

try
{
    if (args.Length == 2 && args[0] == "inspect")
    {
        var inspectPath = Path.GetFullPath(args[1]);
        if (!File.Exists(inspectPath))
            throw new ConversionException($"Input file does not exist: {inspectPath}");
        var info = Spine38BinaryInspector.Inspect(await File.ReadAllBytesAsync(inspectPath));
        var setup = Spine38BinarySetupInspector.Inspect(await File.ReadAllBytesAsync(inspectPath));
        var skins = Spine38BinarySkinInspector.Inspect(await File.ReadAllBytesAsync(inspectPath));
        var animationInfo = Spine38BinaryAnimationInspector.Inspect(await File.ReadAllBytesAsync(inspectPath));
        Console.WriteLine($"Spine version: {info.Version}");
        Console.WriteLine($"Bounds: {info.X}, {info.Y}, {info.Width}, {info.Height}");
        Console.WriteLine($"Shared strings: {info.SharedStringCount}");
        Console.WriteLine($"Header bytes: {info.HeaderByteCount} / {info.FileByteCount}");
        Console.WriteLine(
            $"Setup: bones={setup.BoneCount}, slots={setup.SlotCount}, ik={setup.IkConstraintCount}, " +
            $"transform={setup.TransformConstraintCount}, path={setup.PathConstraintCount}");
        Console.WriteLine($"Skin section offset: {setup.SkinSectionOffset} / {setup.FileByteCount}");
        Console.WriteLine(
            $"Content: skins={skins.SkinCount}, attachments={skins.AttachmentCount}, " +
            $"weighted={skins.WeightedAttachmentCount}, events={skins.EventCount}, animations={skins.AnimationCount}");
        Console.WriteLine("Attachment types: " + string.Join(", ", skins.AttachmentTypes.Select(pair => $"{pair.Key}={pair.Value}")));
        Console.WriteLine($"First animation offset: {skins.FirstAnimationOffset} / {skins.FileByteCount}");
        Console.WriteLine(
            $"Animations validated: timelines={animationInfo.TimelineCount}, frames={animationInfo.FrameCount}, " +
            $"final={animationInfo.FinalOffset}/{animationInfo.FileByteCount}");
        return 0;
    }
    if (args.Length != 4 || args[2] != "-v")
        throw new ConversionException(
            "Usage: SpineConverter <input.json|input.skel> <output.json|output.skel> -v <target-version>\n" +
            "       SpineConverter inspect <input.skel>");

    var inputPath = Path.GetFullPath(args[0]);
    var outputPath = Path.GetFullPath(args[1]);
    if (!File.Exists(inputPath))
        throw new ConversionException($"Input file does not exist: {inputPath}");
    var outputIsJson = outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    var outputIsSkel = outputPath.EndsWith(".skel", StringComparison.OrdinalIgnoreCase);
    if (!outputIsJson && !outputIsSkel)
        throw new ConversionException("Output must be a Spine .json or .skel file.");

    var converter = new JsonSkeletonConverter();
    string sourceJson;
    if (inputPath.EndsWith(".skel", StringComparison.OrdinalIgnoreCase))
    {
        var root = new Spine38BinaryJsonReader().Read(await File.ReadAllBytesAsync(inputPath));
        sourceJson = root.ToJsonString(new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        });
        Console.WriteLine($"Detected input Spine version: {root["skeleton"]!["spine"]!.GetValue<string>()}");
    }
    else if (inputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
        sourceJson = await File.ReadAllTextAsync(inputPath);
    }
    else
    {
        throw new ConversionException("Input must be a Spine .skel or .json file.");
    }
    var result = converter.Convert(sourceJson, SpineVersion.Parse(args[3]));
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    if (outputIsJson)
    {
        await File.WriteAllTextAsync(outputPath, result, new UTF8Encoding(false));
    }
    else
    {
        var convertedRoot = JsonNode.Parse(result)?.AsObject()
            ?? throw new ConversionException("Converted skeleton JSON has no root object.");
        await File.WriteAllBytesAsync(outputPath, new Spine42BinaryWriter().Write(convertedRoot));
    }
    Console.WriteLine($"Converted: {inputPath}");
    Console.WriteLine($"Output: {outputPath}");
    return 0;
}
catch (ConversionException exception)
{
    Console.Error.WriteLine($"Conversion failed: {exception.Message}");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Unexpected failure: {exception.Message}");
    return 1;
}
