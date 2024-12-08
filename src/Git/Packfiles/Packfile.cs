using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression;

namespace codecrafters_git.Git.Packfiles;

// Inspired by https://dev.to/calebsander/git-internals-part-2-packfiles-1jg8
public class Packfile
{
    // Each byte contributes 7 bits of data
    private const byte VarintEncodingBits = 7;

    // The upper bit indicates whether there are more bytes
    private const byte VarintContinueFlag = 1 << VarintEncodingBits;

    // The number of bits storing the object type
    private const byte TypeBits = 3;

    // The number of bits of the object size in the first byte.
    // Each additional byte has VarintEncodingBits of size.
    private const byte TypeByteSizeBits = VarintEncodingBits - TypeBits;

    // Indicates if the delta instruction is a copy (1) or insert (0) operation
    private const byte CopyInstructionFlag = 1 << 7;

    // Number of bytes used to encode the size in a copy instruction
    private const byte CopySizeBytes = 3;

    // gitformat-pack.txt (Instruction to copy from base object): `There is another exception: size zero is automatically converted to 0x10000.`
    private const int CopyZeroSize = 0x10000;

    // Number of bytes used to encode the offset in a copy instruction
    private const byte CopyOffsetBytes = 4;

    // Length of SHA-1 hash in bytes
    private const int HashBytesLength = 20;

    public Packfile(byte[] bytes)
    {
        Bytes = bytes;
        Head = Encoding.ASCII.GetString(bytes[..8]);
        Signature = Encoding.ASCII.GetString(bytes[8..12]);
        Version = BinaryPrimitives.ReadInt32BigEndian(bytes[12..16]);
        ObjectCount = BinaryPrimitives.ReadInt32BigEndian(bytes[16..20]);
        ContentBytes = bytes[20..];
    }

    public string Head { get; }

    public string Signature { get; }

    public int Version { get; }

    public int ObjectCount { get; }

    public byte[] Bytes { get; }

    public byte[] ContentBytes { get; }

    private readonly Dictionary<string, (PackObjectType PackObjectType, byte[] InflatedData)> _gitObjects = new();

    public List<(PackObjectType PackObjectType, byte[] InflatedData)> Decompress()
    {
        using MemoryStream packFileReader = new(ContentBytes);

        for (var i = 0; i < ObjectCount; i++)
        {
            var (objectType, size) = ReadTypeAndSize(packFileReader);

            switch (objectType)
            {
                case PackObjectType.Commit:
                case PackObjectType.Tree:
                case PackObjectType.Blob:
                    HandleUndeltifiedPackObject(size, objectType, packFileReader);
                    break;
                case PackObjectType.RefDelta:
                    HandleDeltifiedPackObject(size, packFileReader);
                    break;
                case PackObjectType.OfsDelta or PackObjectType.Tag:
                    throw new NotSupportedException($"{nameof(objectType)} not implemented");
            }
        }

        return _gitObjects.Values.ToList();
    }

    private void HandleUndeltifiedPackObject(int size, PackObjectType packObjectType, Stream packFileReader)
    {
        var inflated = Inflate(size, packFileReader);

        var header = Encoding.ASCII.GetBytes($"{packObjectType.ToString().ToLower()} {size}\0");
        var fullObject = header.Concat(inflated).ToArray();
        Encoding.ASCII.GetString(fullObject);

        var hash = SHA1.HashData(fullObject);
        var hashString = Convert.ToHexString(hash).ToLower();
        _gitObjects[hashString] = (packObjectType, inflated);
    }

    private void HandleDeltifiedPackObject(int size, Stream packFileReader)
    {
        var hashBytes = new byte[HashBytesLength];
        _ = packFileReader.Read(hashBytes);
        var baseHash = Convert.ToHexString(hashBytes).ToLower();

        var inflated = Inflate(size, packFileReader);

        using MemoryStream inflatedDataStream = new(inflated);

        ReadSizeEncoding(inflatedDataStream);
        var newObjectSize = ReadSizeEncoding(inflatedDataStream);

        if (!_gitObjects.TryGetValue(baseHash, out var baseObject))
            throw new Exception("Missing base object when handling ref delta");

        List<byte> decompressedObject = new(newObjectSize);
        while (ApplyDeltaInstruction(inflatedDataStream, baseObject.InflatedData, decompressedObject))
        {
        }

        var header = Encoding.ASCII.GetBytes($"blob {decompressedObject.Count}\0");
        var fullObject = header.Concat(decompressedObject).ToArray();
        Encoding.ASCII.GetString(fullObject);

        var newHash = SHA1.HashData(fullObject);
        var hashString = Convert.ToHexString(newHash).ToLower();
        _gitObjects[hashString] = (PackObjectType.RefDelta, decompressedObject.ToArray());
    }

    private static byte[] Inflate(int size, Stream packFileReader)
    {
        var startPosition = packFileReader.Position;
        var bufferSize = packFileReader.Length - startPosition;
        var buffer = new byte[bufferSize];
        _ = packFileReader.Read(buffer);

        Inflater inflater = new();
        inflater.SetInput(buffer);

        var inflated = new byte[size];
        inflater.Inflate(inflated);

        // Skip 4-byte CRC32 checksum after each compressed object in a packfile
        packFileReader.Position = startPosition + inflater.TotalIn + 4;

        // When an object has zero size, Git includes 2 additional bytes in the compressed data stream. So skip it.
        if (size == 0) packFileReader.Position += 2;

        return inflated;
    }

    // Reads a single delta instruction from a stream
    // and appends the relevant bytes to `result`.
    // Returns whether the delta stream still had instructions.
    private static bool ApplyDeltaInstruction(Stream stream, byte[] baseData, List<byte> result)
    {
        var instructionBytes = new byte[1];
        var bytesRead = stream.Read(instructionBytes, 0, 1);

        // Check if the stream has ended, meaning the new object is done
        if (bytesRead == 0)
            return false;

        var instruction = instructionBytes[0];

        if ((instruction & CopyInstructionFlag) == 0)
        {
            // Appending 0 bytes doesn't make sense, so git disallows it
            if (instruction == 0)
                throw new InvalidDataException("Invalid data instruction");

            // Append the provided bytes
            var data = new byte[instruction];
            _ = stream.Read(data, 0, instruction);
            result.AddRange(data);
        }
        else
        {
            // Copy instruction
            var nonzeroBytes = instruction;
            var offset = ReadPartialInt(stream, CopyOffsetBytes, ref nonzeroBytes);
            var size = ReadPartialInt(stream, CopySizeBytes, ref nonzeroBytes);

            if (size == 0)
                size = CopyZeroSize; // Copying 0 bytes doesn't make sense, so git assumes a different size

            if (offset + size > baseData.Length)
                throw new InvalidDataException("Invalid copy instruction");

            // Copy bytes from the base object
            result.AddRange(baseData.Skip(offset).Take(size));
        }

        return true;
    }

    // Read an integer of up to `bytes` bytes.
    // `presentBytes` indicates which bytes are provided. The others are 0.    
    private static int ReadPartialInt(Stream stream, byte bytes, ref byte presentBytes)
    {
        var value = 0;

        for (byte byteIndex = 0; byteIndex < bytes; byteIndex++)
        {
            // Use one bit of `presentBytes` to determine if the byte exists
            if ((presentBytes & 1) != 0)
            {
                var byteData = new byte[1];
                _ = stream.Read(byteData, 0, 1);
                value |= (byteData[0] << (byteIndex * 8));
            }

            presentBytes >>= 1;
        }

        return value;
    }

    private static (PackObjectType PackObjectType, int Size) ReadTypeAndSize(Stream packFileReader)
    {
        // Object type and uncompressed pack data size
        // are stored in a "size-encoding" variable-length integer.
        // Bits 4 through 6 store the type and the remaining bits store the size.
        var value = ReadSizeEncoding(packFileReader);

        // Extract the object type (bits 4 through 6)
        var objectType = (PackObjectType)KeepBits(value >> TypeByteSizeBits, TypeBits);

        // Extract the size
        var size = KeepBits(value, TypeByteSizeBits)
                   | ((value >> VarintEncodingBits) << TypeByteSizeBits);

        return (objectType, size);
    }

    // Read a "size encoding" variable-length integer.
    // (There's another slightly different variable-length format
    // called the "offset encoding".)
    private static int ReadSizeEncoding(Stream packfileReader)
    {
        var value = 0;
        var length = 0; // the number of bits of data read so far

        while (true)
        {
            (var byteValue, var moreBytes) = ReadVarintByte(packfileReader);

            // Add in the data bits
            value |= byteValue << length;

            // Stop if this is the last byte
            if (!moreBytes)
                return value;

            length += VarintEncodingBits;
        }
    }

    // Read the lower `bits` bits of `value`
    private static int KeepBits(int value, byte bits)
    {
        return value & ((1 << bits) - 1);
    }

    // Read 7 bits of data and a flag indicating whether there are more
    private static (byte Value, bool MoreBytes) ReadVarintByte(Stream packfileReader)
    {
        var buffer = new byte[1];
        var bytesRead = packfileReader.Read(buffer, 0, 1);

        if (bytesRead != 1)
        {
            throw new IOException("Failed to read the required byte from the stream.");
        }

        var byteValue = buffer[0];
        var value = (byte)(byteValue & ~VarintContinueFlag);
        var moreBytes = (byteValue & VarintContinueFlag) != 0;

        return (value, moreBytes);
    }
}