using System.Buffers.Binary;
using System.IO.Compression;

namespace AssetCare.Infrastructure.Json.Seeding;

/// <summary>產生示意用的 PNG（漸層加色塊），讓附件功能有真實檔案可展示。</summary>
public static class PlaceholderImage
{
    public static byte[] Create(Random random, int width = 480, int height = 360)
    {
        byte[] baseColor = [(byte)random.Next(60, 200), (byte)random.Next(60, 200), (byte)random.Next(60, 200)];
        int boxX = random.Next(40, width / 2), boxY = random.Next(40, height / 2);

        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter: none
            for (var x = 0; x < width; x++)
            {
                var inBox = x >= boxX && x < boxX + width / 3 && y >= boxY && y < boxY + height / 3;
                var shade = inBox ? 55 : (x + y) * 50 / (width + height);
                foreach (var channel in baseColor) raw.WriteByte((byte)Math.Min(255, channel + shade));
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw.ToArray());

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 2; // truecolor

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, compressed.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        var crc = Crc(Crc(0xFFFFFFFF, type), data) ^ 0xFFFFFFFF;
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        output.Write(number);
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc;
    }
}
