namespace codecrafters_git.Git.Packfiles;

public abstract record DeltaInstruction;

public record CopyDeltaInstruction(int Offset, int Size) : DeltaInstruction;

public record InsertDeltaInstruction(byte[] Data) : DeltaInstruction;