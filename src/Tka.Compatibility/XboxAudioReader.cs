using System.Buffers.Binary;
using System.Text;

namespace Tka.Compatibility;

// Adapter for the three XACT container readers, not a replacement for unrelated
// BinaryReader users. Original files remain unchanged. Integer fields are BE in
// Xbox XGS/XSB/XWB; strings and byte fields retain their original encoding.
public sealed class XboxAudioReader : BinaryReader
{
    private readonly bool bigEndian;
    private readonly bool xboxSoundBank;
    private readonly long pcmOffset = long.MaxValue;

    public XboxAudioReader(Stream stream) : base(stream)
    {
        if (!stream.CanSeek) throw new NotSupportedException("XACT reader requires a seekable stream.");
        var position = stream.Position;
        Span<byte> magic = stackalloc byte[4];
        stream.ReadExactly(magic);
        var signature = Encoding.ASCII.GetString(magic);
        bigEndian = signature is "FSGX" or "KBDS" or "DNBW";
        xboxSoundBank = signature == "KBDS";
        if (signature == "DNBW")
        {
            // Xbox XACT wavebank version 46: five offset/length segment pairs
            // start at 0x0C. Bank-data segment describes the entry table.
            uint U32(long offset)
            {
                stream.Position = offset;
                Span<byte> bytes = stackalloc byte[4];
                stream.ReadExactly(bytes);
                return BinaryPrimitives.ReadUInt32BigEndian(bytes);
            }
            if (U32(4) != 46) throw new NotSupportedException("Unsupported Xbox wavebank version.");
            var bank = U32(12);
            var metadata = U32(20);
            var count = U32(bank + 4);
            var stride = U32(bank + 72);
            if (count > 4096 || stride != 24 || (U32(bank) & 0x20000) != 0)
                throw new NotSupportedException("Unsupported Xbox wavebank entry layout.");
            for (uint index = 0; index < count; index++)
            {
                var format = U32(metadata + index * stride + 4);
                // wFormatTag bits 0-1 == PCM; wBitsPerSample bit31 == 16-bit.
                if ((format & 3) != 0 || (format & 0x80000000) == 0)
                    throw new NotSupportedException("Only verified Xbox PCM16 wavebank payloads are supported.");
            }
            pcmOffset = U32(44);
            LocalServices.Log($"Xbox XACT reader: {count} PCM16 waves, payload at 0x{pcmOffset:X}.");
        }
        stream.Position = position;
    }

    public override short ReadInt16() => unchecked((short)ReadUInt16());
    public override ushort ReadUInt16()
    {
        if (!bigEndian) return base.ReadUInt16();
        // MonoGame reads the DWORD cue-name table length (XSB offset 0x1E)
        // as a low ushort followed by a discarded ushort. That only works
        // for LE data. Supply the low half here while preserving both reads.
        if (xboxSoundBank && BaseStream.Position == 0x1E)
        {
            Span<byte> sizeBytes = stackalloc byte[4]; BaseStream.ReadExactly(sizeBytes);
            BaseStream.Position -= 2;
            var size = BinaryPrimitives.ReadUInt32BigEndian(sizeBytes);
            if (size > ushort.MaxValue) throw new NotSupportedException("Large XSB cue-name tables need a wider runtime parser.");
            return (ushort)size;
        }
        Span<byte> bytes = stackalloc byte[2]; BaseStream.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }
    public override int ReadInt32() => unchecked((int)ReadUInt32());
    public override uint ReadUInt32()
    {
        if (!bigEndian) return base.ReadUInt32();
        Span<byte> bytes = stackalloc byte[4]; BaseStream.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }
    public override long ReadInt64() => unchecked((long)ReadUInt64());
    public override ulong ReadUInt64()
    {
        if (!bigEndian) return base.ReadUInt64();
        Span<byte> bytes = stackalloc byte[8]; BaseStream.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }
    public override float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());
    public override double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());
    public override byte[] ReadBytes(int count)
    {
        var position = BaseStream.Position;
        var bytes = base.ReadBytes(count);
        if (bigEndian && position >= pcmOffset)
        {
            if ((bytes.Length & 1) != 0) throw new InvalidDataException("Unaligned PCM16 payload.");
            for (var i = 0; i < bytes.Length; i += 2) (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]);
        }
        return bytes;
    }
}
