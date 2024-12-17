using System.Text;

namespace codecrafters_git.Git.Objects.Trees;

public record GitTreeObjectEntry
{
    public GitTreeObjectEntry(string mode, string name, byte[] hash)
    {
        Mode = mode;
        Name = name;
        Hash = hash;
        Bytes = Encoding.ASCII.GetBytes($"{Mode} {Name}\0").Concat(Hash).ToArray();
        HashHexString = Convert.ToHexString(Hash).ToLower();
    }

    public string Mode { get; }
    public string Name { get; }
    public byte[] Hash { get; }
    public byte[] Bytes { get; }
    public string HashHexString { get; }
}