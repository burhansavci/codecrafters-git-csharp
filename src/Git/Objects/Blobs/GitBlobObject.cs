namespace codecrafters_git.Git.Objects.Blobs;

public record GitBlobObject : GitObject
{
    public GitBlobObject(byte[] contentBytes) : base(ObjectType.Blob, contentBytes) { }
}