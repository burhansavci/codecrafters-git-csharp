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

    using var memoryStream = new MemoryStream(compressed);
    using var zLibStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
    using var reader = new StreamReader(zLibStream);

    var blobObject = reader.ReadToEnd();
    var content = blobObject.Split("\0")[1];

    Console.Write(content);
}
else if (command == "hash-object" && commandArg == "-w")
{
    var fileContent = File.ReadAllText(args[2]);

    var blobObject = $"blob {fileContent.Length}\0{fileContent}";

    using var memoryStream = new MemoryStream();
    using var zLibStream = new ZLibStream(memoryStream, CompressionMode.Compress);
    using var writer = new StreamWriter(zLibStream);

    writer.Write(blobObject);
    var hash = SHA1.HashData(Encoding.UTF8.GetBytes(blobObject));
    var sb = new StringBuilder(hash.Length * 2);

    foreach (byte b in hash)
    {
        sb.Append(b.ToString("x2"));
    }

    var content = sb.ToString();
    
    var objectPath = $".git/objects/{content[..2]}/{content[2..]}";
    
    Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);
    File.WriteAllBytes(objectPath, memoryStream.ToArray());
    
    Console.Write(content);
}
else
{
    throw new ArgumentException($"Unknown command {command}");
}