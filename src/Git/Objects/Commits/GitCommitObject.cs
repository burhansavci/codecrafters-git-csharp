using System.Security.Cryptography;
using System.Text;

namespace codecrafters_git.Git.Objects.Commits;

public class GitCommitObject
{
    public GitCommitObject(string treeHash, string parentCommitHash, string commitMessage, GitCommitObjectAuthorEntry author, GitCommitObjectComitterEntry comitter)
    {
        Author = author;
        Comitter = comitter;
        Content = $"tree {treeHash}\nparent {parentCommitHash}\n{Author}\n{Comitter}\n\n{commitMessage}\n";
        Header = $"commit {Content.Length}\0";
        Bytes = Encoding.ASCII.GetBytes(Header + Content);
        Hash = SHA1.HashData(Bytes);
        HashHexString = Convert.ToHexString(Hash).ToLower();
    }

    private GitCommitObject(string content)
    {
        Author = GitCommitObjectAuthorEntry.FromContent(content);
        Comitter = GitCommitObjectComitterEntry.FromContent(content);
        Content = content;
        Header = $"commit {Content.Length}\0";
        Bytes = Encoding.ASCII.GetBytes(Header + Content);
        Hash = SHA1.HashData(Bytes);
        HashHexString = Convert.ToHexString(Hash).ToLower();
    }

    public static GitCommitObject FromContent(string content) => new(content);

    public string Content { get; }
    public string Header { get; }
    public byte[] Bytes { get; }
    public byte[] Hash { get; }
    public string HashHexString { get; }
    public string Path => $".git/objects/{HashHexString[..2]}/{HashHexString[2..]}";

    public GitCommitObjectAuthorEntry Author { get; }
    public GitCommitObjectComitterEntry Comitter { get; }
}