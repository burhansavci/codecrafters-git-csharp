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
    var fileContent = File.ReadAllText(args[2]);

    var hash = WriteBlobObject(fileContent);

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
    var treeObjectBuilder = new StringBuilder();
    var treeObjectBody = IterateDirectory(currentDirectory, treeObjectBuilder);

    var treeObject = $"tree {treeObjectBody.Length}\0{treeObjectBody}";

    /*
     *   tree <size>\0<mode> <name>\0<20_byte_sha><mode> <name>\0<20_byte_sha>
     */
    Console.WriteLine($"treeObject: {treeObject}");
    var treeObjectBytes = Encoding.UTF8.GetBytes(treeObject);

    var hash = HashFromByteArray(treeObjectBytes);

    Console.WriteLine(hash);
}
else
{
    throw new ArgumentException($"Unknown command {command}");
}

string IterateDirectory(string directory, StringBuilder treeObjectBody)
{
    var directories = Directory.GetDirectories(directory).Where(x => !x.EndsWith(".git"));
    foreach (var dir in directories)
    {
        var treeObjectRow = $"040000 {Path.GetFileName(dir)}\0{HashFromString(IterateDirectory(dir, treeObjectBody))}";
        treeObjectBody.Append(treeObjectRow);
    }

    treeObjectBody.Append(WriteAllFiles(directory));
    return treeObjectBody.ToString();
}

string WriteAllFiles(string directory)
{
    var files = Directory.GetFiles(directory);
    var treeBlobObjectBody = new StringBuilder();
    foreach (var file in files)
    {
        var fileContent = File.ReadAllText(file);
        var hash = WriteBlobObject(fileContent);
        var treeObjectRow = $"100644 {Path.GetFileName(file)}\0{hash}";
        treeBlobObjectBody.Append(treeObjectRow);
    }

    return treeBlobObjectBody.ToString();
}

string WriteBlobObject(string fileContent)
{
    var blobObject = $"blob {fileContent.Length}\0{fileContent}";
    var blobObjectBytes = Encoding.UTF8.GetBytes(blobObject);

    var hash = HashFromByteArray(blobObjectBytes);

    var objectPath = $".git/objects/{hash[..2]}/{hash[2..]}";

    Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);
    File.WriteAllBytes(objectPath, Compress(blobObjectBytes));

    return hash;
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

string HashFromByteArray(byte[] data)
{
    var hash = SHA1.HashData(data);
    var sb = new StringBuilder(hash.Length * 2);
    foreach (byte b in hash)
    {
        sb.Append(b.ToString("x2"));
    }

    return sb.ToString();
}

string HashFromString(string data)
{
    var hash = SHA1.HashData(Encoding.UTF8.GetBytes(data));
    var sb = new StringBuilder(hash.Length * 2);
    foreach (byte b in hash)
    {
        sb.Append(b.ToString("x2"));
    }

    return sb.ToString();
}