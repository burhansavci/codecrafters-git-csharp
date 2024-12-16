using System.Security.Cryptography;
using System.Text;
using codecrafters_git.Git.Extensions;

namespace codecrafters_git.Git.Objects;

public record GitObject(ObjectType Type, byte[] ContentBytes)
{
    private static readonly byte[] SpaceBytes = [(byte)' '];
    private static readonly byte[] NullBytes = [0];

    public static GitObject FromHashHexString(string hashHexString)
    {
        var path = $".git/objects/{hashHexString[..2]}/{hashHexString[2..]}";

        if (!File.Exists(path))
            throw new ArgumentException($"Object {hashHexString} not found.");

        var compressed = File.ReadAllBytes(path);
        var decompressed = compressed.DeCompress();

        var headerSpaceIndex = Array.IndexOf(decompressed, SpaceBytes[0]);
        var headerNullIndex = Array.IndexOf(decompressed, NullBytes[0]);

        var typeInString = Encoding.ASCII.GetString(decompressed[..headerSpaceIndex]);
        var type = typeInString.ToObjectType();

        return new GitObject(type, decompressed[(headerNullIndex + 1)..]);
    }

    public string Write()
    {
        byte[] lengthInBytes = Encoding.ASCII.GetBytes(ContentBytes.Length.ToString());
        byte[] typeInBytes = Encoding.ASCII.GetBytes(Type.ToString().ToLower());

        using MemoryStream memoryStream = new();

        memoryStream.Write(typeInBytes);
        memoryStream.Write(SpaceBytes);
        memoryStream.Write(lengthInBytes);
        memoryStream.Write(NullBytes);
        memoryStream.Write(ContentBytes);

        return Write(memoryStream.ToArray());
    }

    private static string Write(byte[] bytes)
    {
        byte[] hash = SHA1.HashData(bytes);
        var hashHexString = Convert.ToHexString(hash).ToLower();

        var path = $".git/objects/{hashHexString[..2]}/{hashHexString[2..]}";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes.Compress());

        return hashHexString;
    }
}