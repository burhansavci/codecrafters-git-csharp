using System.Security.Cryptography;
using System.Text;
using codecrafters_git.Git.Extensions;

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

    public string Content { get; }
    public string Header { get; }
    public byte[] Bytes { get; }
    public byte[] Hash { get; }
    public string HashHexString { get; }
    public string TreeHashHexString => Content.Split('\n')[0].Split(' ')[1];
    public string Path => $".git/objects/{HashHexString[..2]}/{HashHexString[2..]}";
    public GitCommitObjectAuthorEntry Author { get; }
    public GitCommitObjectComitterEntry Comitter { get; }

    public static GitCommitObject FromHashHexString(string hashHexString)
    {
        var path = $".git/objects/{hashHexString[..2]}/{hashHexString[2..]}";

        if (!File.Exists(path))
            throw new ArgumentException($"Object {hashHexString} not found.");

        var compressed = File.ReadAllBytes(path);
        var decompressed = compressed.DeCompress();

        var headerNullIndex = Array.IndexOf(decompressed, (byte)0);
        //Skip: commit <size>\0
        var content = Encoding.ASCII.GetString(decompressed[(headerNullIndex + 1)..]);

        return FromContent(content);
    }

    public static GitCommitObject FromContent(string content) => new(content);
}