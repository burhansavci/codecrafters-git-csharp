using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

if (args.Length < 1)
{
    Console.WriteLine("Please provide a command.");
    return;
}

string command = args[0];
string? commandArg = args.Length > 1 ? args[1] : null;

if (command == "init")
{
    Directory.CreateDirectory(".git");
    Directory.CreateDirectory(".git/objects");
    Directory.CreateDirectory(".git/refs");
    File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
    Console.WriteLine("Initialized git directory");
}
else if (command == "cat-file" && commandArg == "-p")
{
    var hash = args[2];
    var objectPath = $".git/objects/{hash[..2]}/{hash[2..]}";

    if (!File.Exists(objectPath))
    {
        Console.WriteLine($"Object {hash} not found.");
        return;
    }

    var compressed = File.ReadAllBytes(objectPath);
    var blobObject = DeCompress(compressed);

    var content = blobObject.Split("\0")[1];

    Console.Write(content);
}
else if (command == "hash-object" && commandArg == "-w")
{
    var (hash, _) = WriteBlobObject(args[2]);

    Console.Write(hash);
}
else if (command == "ls-tree" && commandArg == "--name-only")
{
    var hash = args[2];
    var treePath = $".git/objects/{hash[..2]}/{hash[2..]}";

    if (!File.Exists(treePath))
    {
        Console.WriteLine($"Tree {hash} not found.");
        return;
    }

    var compressed = File.ReadAllBytes(treePath);
    var treeObject = DeCompress(compressed);

    var lines = treeObject.Split(" ");
    var names = new List<string>();
    for (var i = 2; i < lines.Length; i++)
    {
        var name = lines[i].Split("\0")[0];
        names.Add(name);
    }

    foreach (var name in names.OrderBy(x => x))
    {
        Console.WriteLine(name);
    }
}
else if (command == "write-tree")
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var (hash, _) = WriteTreeObject(currentDirectory);

    Console.WriteLine(hash);
}
else
{
    throw new ArgumentException($"Unknown command {command}");
}

(string hash, string hashWithoutHex) WriteTreeObject(string directory)
{
    var directories = Directory.GetDirectories(directory).Where(x => !x.EndsWith(".git"));
    var files = Directory.GetFiles(directory);
    var directoryAndFiles = directories.Concat(files).OrderBy(x => x);

    var treeObjectBody = new StringBuilder();
    foreach (var directoryAndFile in directoryAndFiles)
    {
        if (Directory.Exists(directoryAndFile))
        {
            var treeObjectRow = $"040000 {Path.GetFileName(directoryAndFile)}\0{WriteTreeObject(directoryAndFile).hashWithoutHex}";
            treeObjectBody.Append(treeObjectRow);
        }
        else
        {
            var (_, blobHashWithoutHex) = WriteBlobObject(directoryAndFile);
            var treeObjectRow = $"100644 {Path.GetFileName(directoryAndFile)}\0{blobHashWithoutHex}";
            treeObjectBody.Append(treeObjectRow);
        }
    }

    var treeObject = $"tree {treeObjectBody.Length}\0{treeObjectBody}";
    var treeObjectBytes = Encoding.UTF8.GetBytes(treeObject);
    var treeHash = Hash(treeObjectBytes);
    var treeObjectPath = $".git/objects/{treeHash[..2]}/{treeHash[2..]}";

    Directory.CreateDirectory(Path.GetDirectoryName(treeObjectPath)!);
    File.WriteAllBytes(treeObjectPath, Compress(treeObjectBytes));

    return (treeHash, HashWithoutHex(treeObjectBytes));
}


(string hash, string hashWithoutHex) WriteBlobObject(string filePath)
{
    var fileContent = File.ReadAllText(filePath);
    var blobObject = $"blob {fileContent.Length}\0{fileContent}";
    var blobObjectBytes = Encoding.UTF8.GetBytes(blobObject);

    var hash = Hash(blobObjectBytes);
    var hashWithoutHex = HashWithoutHex(blobObjectBytes);

    var blobObjectPath = $".git/objects/{hash[..2]}/{hash[2..]}";

    Directory.CreateDirectory(Path.GetDirectoryName(blobObjectPath)!);
    File.WriteAllBytes(blobObjectPath, Compress(blobObjectBytes));

    return (hash, hashWithoutHex);
}

byte[] Compress(byte[] data)
{
    using var memoryStream = new MemoryStream();
    using (var zlibStream = new ZLibStream(memoryStream, CompressionMode.Compress))
        zlibStream.Write(data, 0, data.Length);

    return memoryStream.ToArray();
}

string DeCompress(byte[] data)
{
    using var memoryStream = new MemoryStream(data);
    using var zLibStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
    using var reader = new StreamReader(zLibStream);

    return reader.ReadToEnd();
}

string Hash(byte[] data)
{
    var hash = SHA1.HashData(data);
    var sb = new StringBuilder(hash.Length * 2);
    foreach (byte b in hash)
    {
        sb.Append(b.ToString("x2"));
    }

    return sb.ToString();
}

string HashWithoutHex(byte[] data)
{
    var hash = SHA1.HashData(data);
    var sb = new StringBuilder(hash.Length);
    foreach (byte b in hash)
    {
        sb.Append(b);
    }
    return sb.ToString();
}