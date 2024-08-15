using System.Security.Cryptography;
using System.Text;

namespace codecrafters_git.Git.Objects.Commits;

public class GitCommitObject
{
    public GitCommitObject(string treeHash, string parentCommitHash, string commitMessage)
    {
        Content = $"tree {treeHash}\nparent {parentCommitHash}\n{Author}\n{Comitter}\n\n{commitMessage}\n";
        Header = $"commit {Content.Length}\0";
        Bytes = Encoding.ASCII.GetBytes(Header + Content);
        Hash = SHA1.HashData(Bytes);
        HashHexString = Convert.ToHexString(Hash).ToLower();
    }

    public string Content { get; }
    public string Header { get; }
    public byte[] Bytes { get; set; }
    public byte[] Hash { get; set; }
    public string HashHexString { get; set; }
    public string Path => $".git/objects/{HashHexString[..2]}/{HashHexString[2..]}";
    
    public GitCommitObjectAuthorEntry Author { get; } = new();
    public GitCommitObjectComitterEntry Comitter { get; } = new();
}