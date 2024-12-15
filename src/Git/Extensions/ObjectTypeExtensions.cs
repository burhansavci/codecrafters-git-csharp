using codecrafters_git.Git.Objects;

namespace codecrafters_git.Git.Extensions;

public static class ObjectTypeExtensions
{
    public static ObjectType ToObjectType(this string type)
    {
        return type switch
        {
            "commit" => ObjectType.Commit,
            "tree" => ObjectType.Tree,
            "blob" => ObjectType.Blob,
            "tag" => ObjectType.Tag,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}