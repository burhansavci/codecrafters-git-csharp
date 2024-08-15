using System.IO.Compression;

namespace codecrafters_git.Git.Extensions;

public static class ZlibExtensions
{
    public static byte[] Compress(this byte[] data)
    {
        using var memoryStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(memoryStream, CompressionMode.Compress))
            zlibStream.Write(data, 0, data.Length);

        return memoryStream.ToArray();
    }

    public static byte[] DeCompress(this byte[] data)
    {
        using var memoryStream = new MemoryStream(data);
        using var zLibStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();

        zLibStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }
}