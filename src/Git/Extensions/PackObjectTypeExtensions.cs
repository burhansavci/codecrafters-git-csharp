using codecrafters_git.Git.Objects;
using codecrafters_git.Git.Packfiles;

namespace codecrafters_git.Git.Extensions;

public  static class PackObjectTypeExtensions
{
    public static ObjectType ToObjectType(this PackObjectType packObjectType)
    {
        return packObjectType switch
        {
            PackObjectType.Commit => ObjectType.Commit,
            PackObjectType.Tree => ObjectType.Tree,
            PackObjectType.Blob => ObjectType.Blob,
            PackObjectType.Tag => ObjectType.Tag,
            _ => throw new ArgumentException($"Unsupported pack object type: {packObjectType}", nameof(packObjectType))
        };
    }
}