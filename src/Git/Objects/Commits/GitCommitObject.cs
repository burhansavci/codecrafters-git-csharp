using System.Text;

namespace codecrafters_git.Git.Objects.Commits;

public record GitCommitObject : GitObject
{
    public GitCommitObject(string treeHash, string parentCommitHash, string commitMessage, GitCommitObjectAuthorEntry author, GitCommitObjectComitterEntry comitter)
        : base(ObjectType.Commit, GetContentBytes(treeHash, parentCommitHash, commitMessage, author, comitter))
    {
        Author = author;
        Comitter = comitter;
        TreeHashHexString = treeHash;
    }

    public GitCommitObject(byte[] contentBytes) : base(ObjectType.Commit, contentBytes)
    {
        var content = Encoding.ASCII.GetString(contentBytes);
        Author = GitCommitObjectAuthorEntry.FromContent(content);
        Comitter = GitCommitObjectComitterEntry.FromContent(content);
        TreeHashHexString = content.Split('\n')[0].Split(' ')[1];
    }

    public string TreeHashHexString { get; }
    public GitCommitObjectAuthorEntry Author { get; }
    public GitCommitObjectComitterEntry Comitter { get; }

    private static byte[] GetContentBytes(string treeHash, string parentCommitHash, string commitMessage, GitCommitObjectAuthorEntry author, GitCommitObjectComitterEntry comitter)
    {
        var content = $"tree {treeHash}\nparent {parentCommitHash}\n{author}\n{comitter}\n\n{commitMessage}\n";
        return Encoding.ASCII.GetBytes(content);
    }
}