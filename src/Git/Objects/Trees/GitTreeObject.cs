using System.Text;

namespace codecrafters_git.Git.Objects.Trees;

public record GitTreeObject : GitObject
{
    public GitTreeObject(List<GitTreeObjectEntry> entries)
        : base(ObjectType.Tree, entries.Select(x => x.Bytes).Aggregate((x, y) => x.Concat(y).ToArray()))
    {
        Entries = entries.OrderBy(x => x.Name).ToList();
    }

    public GitTreeObject(byte[] contentBytes)
        : base(ObjectType.Tree, contentBytes)
    {
        Entries = ParseEntries(contentBytes).OrderBy(x => x.Name).ToList();
    }

    public List<GitTreeObjectEntry> Entries { get; }

    private static List<GitTreeObjectEntry> ParseEntries(byte[] contentBytes)
    {
        var entries = new List<GitTreeObjectEntry>();
        var currentIndex = 0;

        // Parse each: <mode> <name>\0<20_byte_sha>
        while (currentIndex < contentBytes.Length)
        {
            var spaceIndex = Array.IndexOf(contentBytes, SpaceByte, currentIndex);
            var nullIndex = Array.IndexOf(contentBytes, NullByte, spaceIndex);
            var mode = Encoding.ASCII.GetString(contentBytes[currentIndex..spaceIndex]);
            var name = Encoding.ASCII.GetString(contentBytes[(spaceIndex + 1)..nullIndex]);
            var hash = contentBytes[(nullIndex + 1)..(nullIndex + 1 + 20)];

            entries.Add(new GitTreeObjectEntry(mode, name, hash));
            currentIndex = nullIndex + 1 + 20;
        }

        return entries;
    }
}