using System.Buffers.Binary;
using System.Text;

namespace SpineConverter.Core;

internal sealed class SpineBinaryInput(ReadOnlyMemory<byte> data)
{
    private readonly ReadOnlyMemory<byte> _data = data;
    public int Position { get; private set; }
    public int Remaining => _data.Length - Position;

    public void Skip(int count)
    {
        Ensure(count);
        Position += count;
    }

    public byte ReadByte()
    {
        Ensure(1);
        return _data.Span[Position++];
    }

    public bool ReadBoolean() => ReadByte() != 0;

    public int ReadInt32()
    {
        Ensure(4);
        var result = BinaryPrimitives.ReadInt32BigEndian(_data.Span[Position..]);
        Position += 4;
        return result;
    }

    public ushort ReadUInt16()
    {
        Ensure(2);
        var result = BinaryPrimitives.ReadUInt16BigEndian(_data.Span[Position..]);
        Position += 2;
        return result;
    }

    public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

    public int ReadVarInt(bool optimizePositive)
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            var current = ReadByte();
            value |= (uint)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
            {
                if (optimizePositive)
                    return unchecked((int)value);
                return unchecked((int)((value >> 1) ^ (uint)-(int)(value & 1)));
            }
        }
        throw new ConversionException($"Invalid varint at offset {Position}.");
    }

    public string? ReadString()
    {
        var encodedLength = ReadVarInt(true);
        if (encodedLength == 0)
            return null;
        var byteCount = encodedLength - 1;
        if (byteCount == 0)
            return string.Empty;
        Ensure(byteCount);
        try
        {
            var result = new UTF8Encoding(false, true).GetString(_data.Span.Slice(Position, byteCount));
            Position += byteCount;
            return result;
        }
        catch (DecoderFallbackException exception)
        {
            throw new ConversionException($"Invalid UTF-8 string at offset {Position}: {exception.Message}");
        }
    }

    private void Ensure(int count)
    {
        if (count < 0 || Remaining < count)
            throw new ConversionException(
                $"Unexpected end of Spine binary at offset {Position}; need {count} bytes, have {Remaining}.");
    }
}
