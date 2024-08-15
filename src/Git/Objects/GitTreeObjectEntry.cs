using System.Text;

namespace codecrafters_git.Git.Objects;

record GitTreeObjectEntry(string Mode, string Name, byte[] Hash)
{
    public byte[] Bytes => Encoding.ASCII.GetBytes($"{Mode} {Name}\0").Concat(Hash).ToArray();
}