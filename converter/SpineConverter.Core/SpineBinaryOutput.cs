using System.Buffers.Binary;
using System.Text;

namespace SpineConverter.Core;

internal sealed class SpineBinaryOutput
{
    private readonly MemoryStream _stream = new();

    public void WriteByte(int value) => _stream.WriteByte(unchecked((byte)value));
    public void WriteBoolean(bool value) => WriteByte(value ? 1 : 0);

    public void WriteInt32(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        _stream.Write(bytes);
    }

    public void WriteInt64(long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        _stream.Write(bytes);
    }

    public void WriteSingle(float value) => WriteInt32(BitConverter.SingleToInt32Bits(value));

    public void WriteVarInt(int value, bool optimizePositive)
    {
        var encoded = optimizePositive
            ? unchecked((uint)value)
            : unchecked((uint)((value << 1) ^ (value >> 31)));
        while (true)
        {
            var current = (byte)(encoded & 0x7f);
            encoded >>= 7;
            if (encoded == 0)
            {
                WriteByte(current);
                return;
            }
            WriteByte(current | 0x80);
        }
    }

    public void WriteString(string? value)
    {
        if (value is null)
        {
            WriteVarInt(0, true);
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(bytes.Length + 1, true);
        _stream.Write(bytes);
    }

    public byte[] ToArray() => _stream.ToArray();
}
