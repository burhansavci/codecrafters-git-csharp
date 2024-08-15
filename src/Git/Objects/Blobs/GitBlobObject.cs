using System.Security.Cryptography;
using System.Text;
using codecrafters_git.Git.Extensions;

namespace codecrafters_git.Git.Objects.Blobs;

record GitBlobObject
{
    private GitBlobObject(string content, string header, byte[] bytes, byte[] hash, string hashHexString, string path)
    {
        Content = content;
        Header = header;
        Bytes = bytes;
        Hash = hash;
        HashHexString = hashHexString;
        Path = path;
    }

    private GitBlobObject(string content)
    {
        Content = content;
        Header = $"blob {content.Length}\0";
        Bytes = Encoding.ASCII.GetBytes(Header + content);
        Hash = SHA1.HashData(Bytes);
        HashHexString = Convert.ToHexString(Hash).ToLower();
        Path = $".git/objects/{HashHexString[..2]}/{HashHexString[2..]}";
    }

    public string Content { get; }
    public string Header { get; }
    public byte[] Bytes { get; }
    public byte[] Hash { get; }
    public string HashHexString { get; }
    public string Path { get; }

    public static GitBlobObject FromContent(string content) => new(content);

    public static GitBlobObject FromHashHexString(string hashHexString)
    {
        var path = $".git/objects/{hashHexString[..2]}/{hashHexString[2..]}";

        if (!File.Exists(path))
            throw new ArgumentException($"Object {hashHexString} not found.");

        var compressed = File.ReadAllBytes(path);
        var decompressed = compressed.DeCompress();

        var headerNullIndex = Array.IndexOf(decompressed, (byte)0);
        //Skip: blob <size>\0
        var content = Encoding.ASCII.GetString(decompressed[(headerNullIndex + 1)..]);

        return new GitBlobObject(content, $"blob {content.Length}\0", decompressed, compressed, hashHexString, path);
    }
}