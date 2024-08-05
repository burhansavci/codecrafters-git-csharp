using System.IO.Compression;

if (args.Length < 1)
{
    Console.WriteLine("Please provide a command.");
    return;
}

string command = args[0];

if (command == "init")
{
    // Uncomment this block to pass the first stage
    Directory.CreateDirectory(".git");
    Directory.CreateDirectory(".git/objects");
    Directory.CreateDirectory(".git/refs");
    File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
    Console.WriteLine("Initialized git directory");
}
else if (command == "cat-file")
{
    var commandArg = args[1];

    if (commandArg == "-p")
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
        
        Console.Write(reader.ReadToEnd());
    }
}
else
{
    throw new ArgumentException($"Unknown command {command}");
}