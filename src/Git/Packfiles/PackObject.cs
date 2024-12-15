using codecrafters_git.Git.Objects;

namespace codecrafters_git.Git.Packfiles;

public abstract record PackObject;

public record UnDeltifiedPackObject(ObjectType Type, byte[] Bytes) : PackObject;

public record DeltifiedPackObject(string BaseHashHexString, int Size, List<DeltaInstruction> Instructions) : PackObject;