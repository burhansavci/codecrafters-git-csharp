using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

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

    public List<(ObjectType objectType, int size, string content)> Decompress()
    {
        List<(ObjectType objectType, int size, string content)> objects = [];

        using MemoryStream packFileReader = new(ContentBytes);

        for (int i = 0; i < ObjectCount;)
        {
            var (objectType, size) = ReadTypeAndSize(packFileReader);

            if (objectType is ObjectType.OfsDelta or ObjectType.RefDelta)
            {
                //TODO: Handle OfsDelta and RefDelta
                break;
            }

            // Save the packFileReader position before starting to read the git object
            long packFileReaderBeforeReadPosition = packFileReader.Position;

            List<byte> decompressedObject = new(size);

            using (var decoder = new ZLibStream(packFileReader, CompressionMode.Decompress, leaveOpen: true))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = decoder.Read(buffer, 0, buffer.Length)) != 0)
                    decompressedObject.AddRange(buffer.AsSpan(0, bytesRead));

                long totalBytesRead = packFileReader.Position - packFileReaderBeforeReadPosition;

                // Adjust the packFileReader position for the next git object
                packFileReader.Seek(packFileReaderBeforeReadPosition + totalBytesRead, SeekOrigin.Begin);
            }

            objects.Add((objectType, size, Encoding.ASCII.GetString(decompressedObject.ToArray())));
        }

        return objects;
    }

    private (ObjectType ObjectType, int Size) ReadTypeAndSize(Stream packFileReader)
    {
        // Object type and uncompressed pack data size
        // are stored in a "size-encoding" variable-length integer.
        // Bits 4 through 6 store the type and the remaining bits store the size.
        int value = ReadSizeEncoding(packFileReader);

        // Extract the object type (bits 4 through 6)
        ObjectType objectType = (ObjectType)KeepBits(value >> TypeByteSizeBits, TypeBits);

        // Extract the size
        int size = KeepBits(value, TypeByteSizeBits)
                   | ((value >> VarintEncodingBits) << TypeByteSizeBits);

        return (objectType, size);
    }

    // Read a "size encoding" variable-length integer.
    // (There's another slightly different variable-length format
    // called the "offset encoding".)
    private static int ReadSizeEncoding(Stream packfileReader)
    {
        int value = 0;
        int length = 0; // the number of bits of data read so far

        while (true)
        {
            var (byteValue, moreBytes) = ReadVarintByte(packfileReader);

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
        byte[] buffer = new byte[1];
        int bytesRead = packfileReader.Read(buffer, 0, 1);

        if (bytesRead != 1)
        {
            throw new IOException("Failed to read the required byte from the stream.");
        }

        byte byteValue = buffer[0];
        byte value = (byte)(byteValue & ~VarintContinueFlag);
        bool moreBytes = (byteValue & VarintContinueFlag) != 0;

        return (value, moreBytes);
    }
}