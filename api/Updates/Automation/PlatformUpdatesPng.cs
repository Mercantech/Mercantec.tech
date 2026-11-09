using System.Buffers.Binary;

namespace Mercantec.Tech.Api.Updates.Automation;

internal static class PlatformUpdatesPng
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = CreateTable();

    public static byte[] Decode(PlatformUpdatesPublishImage image)
    {
        if (!PlatformUpdatesAutomationValidation.IsText(image.Alt, 200)
            || string.IsNullOrEmpty(image.Base64) || image.Base64.Length > 2_800_000)
            throw new PlatformUpdatesAutomationException("invalid_update_image");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(image.Base64); }
        catch (FormatException) { throw new PlatformUpdatesAutomationException("invalid_update_image"); }
        if (bytes.Length is < 45 or > MaximumBytes || !bytes.AsSpan(0, 8).SequenceEqual(Signature)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13
            || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new PlatformUpdatesAutomationException("invalid_update_image");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is 0 or > 4096 || height is 0 or > 4096 || (ulong)width * height > 16_777_216)
            throw new PlatformUpdatesAutomationException("invalid_image_dimensions");
        var sawData = false;
        for (var offset = 8; offset < bytes.Length;)
        {
            if (bytes.Length - offset < 12) throw new PlatformUpdatesAutomationException("invalid_update_image");
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > (uint)(bytes.Length - offset - 12)) throw new PlatformUpdatesAutomationException("invalid_update_image");
            var size = (int)length;
            var type = bytes.AsSpan(offset + 4, 4);
            if (!type.ToArray().All(value => value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z'))
                throw new PlatformUpdatesAutomationException("invalid_update_image");
            if (offset != 8 && type.SequenceEqual("IHDR"u8)) throw new PlatformUpdatesAutomationException("invalid_update_image");
            var crc = uint.MaxValue;
            foreach (var value in bytes.AsSpan(offset + 4, size + 4)) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
            if ((crc ^ uint.MaxValue) != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + size, 4)))
                throw new PlatformUpdatesAutomationException("invalid_update_image");
            if (type.SequenceEqual("IDAT"u8)) sawData = true;
            if (type.SequenceEqual("IEND"u8))
            {
                if (!sawData || size != 0 || offset + 12 != bytes.Length)
                    throw new PlatformUpdatesAutomationException("invalid_update_image");
                return bytes;
            }
            offset += size + 12;
        }
        throw new PlatformUpdatesAutomationException("invalid_update_image");
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++) value = (value & 1) == 0 ? value >> 1 : 0xedb88320u ^ (value >> 1);
            table[index] = value;
        }
        return table;
    }
}
