namespace Mercantec.Tech.Api.Emojis;

/// <summary>Copies a GIF's first compressed frame; no pixel decoding or native codec is needed.</summary>
public static class EmojiGifFirstFrame
{
    public static byte[] Extract(ReadOnlySpan<byte> source)
    {
        if (source.Length < 14 || !(source[..6].SequenceEqual("GIF87a"u8) || source[..6].SequenceEqual("GIF89a"u8)))
            throw new ArgumentException("invalid_file_signature");
        var position = 13;
        if ((source[10] & 0x80) != 0) position += 3 * (1 << ((source[10] & 7) + 1));
        Require(source, position);
        var prefixEnd = position;
        var controlStart = -1;
        var controlLength = 0;
        while (position < source.Length)
        {
            var marker = source[position++];
            if (marker == 0x21)
            {
                Require(source, position + 1);
                var start = position - 1;
                var label = source[position++];
                SkipSubBlocks(source, ref position);
                if (label == 0xF9) { controlStart = start; controlLength = position - start; }
            }
            else if (marker == 0x2C)
            {
                var imageStart = position - 1;
                Require(source, position + 9);
                var packed = source[position + 8];
                position += 9;
                if ((packed & 0x80) != 0) position += 3 * (1 << ((packed & 7) + 1));
                Require(source, position + 1);
                position++; // LZW minimum code size.
                SkipSubBlocks(source, ref position);
                using var result = new MemoryStream();
                result.Write(source[..prefixEnd]);
                if (controlStart >= 0) result.Write(source.Slice(controlStart, controlLength));
                result.Write(source.Slice(imageStart, position - imageStart));
                result.WriteByte(0x3B);
                return result.ToArray();
            }
            else break;
        }
        throw new ArgumentException("invalid_file_signature");
    }

    private static void SkipSubBlocks(ReadOnlySpan<byte> source, ref int position)
    {
        while (true)
        {
            Require(source, position + 1);
            var length = source[position++];
            if (length == 0) return;
            position += length;
            Require(source, position);
        }
    }

    private static void Require(ReadOnlySpan<byte> source, int end)
    {
        if (end > source.Length) throw new ArgumentException("invalid_file_signature");
    }
}
