using System.Security.Cryptography;
using System.Text;
using codecrafters_git.Git.Extensions;
using codecrafters_git.Git.Objects.Blobs;
using codecrafters_git.Git.Objects.Commits;
using codecrafters_git.Git.Objects.Trees;

namespace codecrafters_git.Git.Objects;

public record GitObject
{
    protected const byte SpaceByte = (byte)' ';
    protected const byte NullByte = 0;

    public GitObject(ObjectType type, byte[] contentBytes)
    {
        ArgumentNullException.ThrowIfNull(contentBytes);

        Type = type;
        ContentBytes = contentBytes;
        Header = $"{Type.ToString().ToLower()} {ContentBytes.Length}\0";
        Bytes = Encoding.ASCII.GetBytes(Header).Concat(ContentBytes).ToArray();
        Hash = SHA1.HashData(Bytes);
        HashHexString = Convert.ToHexString(Hash).ToLower();
        Path = $".git/objects/{HashHexString[..2]}/{HashHexString[2..]}";
    }

    public ObjectType Type { get; }
    public byte[] ContentBytes { get; }
    public string Header { get; }
    public byte[] Bytes { get; }
    public byte[] Hash { get; }
    public string HashHexString { get; }
    public string Path { get; }

    public static T FromHashHexString<T>(string hashHexString) where T : GitObject
    {
        ArgumentException.ThrowIfNullOrEmpty(hashHexString);

        var path = $".git/objects/{hashHexString[..2]}/{hashHexString[2..]}";

        if (!File.Exists(path))
            throw new FileNotFoundException($"Git object {hashHexString} not found at {path}");

        var compressed = File.ReadAllBytes(path);
        var decompressed = compressed.DeCompress();

        var (type, contentBytes) = ParseObjectData(decompressed);

        GitObject gitObject = type switch
        {
            ObjectType.Blob => new GitBlobObject(contentBytes),
            ObjectType.Tree => new GitTreeObject(contentBytes),
            ObjectType.Commit => new GitCommitObject(contentBytes),
            ObjectType.Tag => throw new NotSupportedException("Tags are not supported yet."),
            _ => throw new ArgumentException($"Unsupported object type: {type}")
        };

        if (gitObject is not T result)
            throw new ArgumentException($"Object {hashHexString} is not of type {typeof(T).Name}");

        return result;
    }

    public void Write()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllBytes(Path, Bytes.Compress());
    }

    private static (ObjectType type, byte[] contentBytes) ParseObjectData(byte[] decompressed)
    {
        //<type> <size>\0<contentBytes>
        var headerSpaceIndex = Array.IndexOf(decompressed, SpaceByte);
        var headerNullIndex = Array.IndexOf(decompressed, NullByte);

        if (headerSpaceIndex == -1 || headerNullIndex == -1)
        {
            throw new FormatException("Invalid git object format");
        }

        var typeString = Encoding.ASCII.GetString(decompressed[..headerSpaceIndex]);
        var type = typeString.ToObjectType();
        var contentBytes = decompressed[(headerNullIndex + 1)..];

        return (type, contentBytes);
    }
}