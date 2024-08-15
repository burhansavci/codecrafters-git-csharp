using System.Security.Cryptography;
using System.Text;
using codecrafters_git.Git.Extensions;

namespace codecrafters_git.Git.Objects;

record GitTreeObject
{
    public string Header => $"tree {Body.Length}\0";
    public byte[] Body => Entries.Select(x => x.Bytes).Aggregate((x, y) => x.Concat(y).ToArray());
    public byte[] Bytes => Encoding.ASCII.GetBytes(Header).Concat(Body).ToArray();
    public byte[] Hash => SHA1.HashData(Bytes);
    public string HashHexString => Convert.ToHexString(Hash).ToLower();
    public string Path => $".git/objects/{HashHexString[..2]}/{HashHexString[2..]}";
    public List<GitTreeObjectEntry> Entries { get; private init; } = [];

    public static GitTreeObject FromHashHexString(string hashHexString)
    {
        var path = $".git/objects/{hashHexString[..2]}/{hashHexString[2..]}";

        if (!File.Exists(path))
            throw new ArgumentException($"Object {hashHexString} not found.");

        var compressed = File.ReadAllBytes(path);
        var decompressed = compressed.DeCompress();

        var headerNullIndex = Array.IndexOf(decompressed, (byte)0);
        //Skip: tree <size>\0
        var hashContent = decompressed[(headerNullIndex + 1)..];

        var entries = new List<GitTreeObjectEntry>();
        var currentIndex = 0;

        // Parse each: <mode> <name>\0<20_byte_sha>
        while (currentIndex < hashContent.Length)
        {
            var spaceIndex = Array.IndexOf(hashContent, (byte)' ', currentIndex);
            var nullIndex = Array.IndexOf(hashContent, (byte)0, spaceIndex);
            var mode = Encoding.ASCII.GetString(hashContent[currentIndex..spaceIndex]);
            var name = Encoding.ASCII.GetString(hashContent[(spaceIndex + 1)..nullIndex]);
            var hash = hashContent[(nullIndex + 1)..(nullIndex + 1 + 20)];

            entries.Add(new GitTreeObjectEntry(mode, name, hash));
            currentIndex = nullIndex + 1 + 20;
        }

        return new GitTreeObject
        {
            Entries = entries.OrderBy(x => x.Name).ToList()
        };
    }
}