using System.Text;
using codecrafters_git.Git.Extensions;
using codecrafters_git.Git.Objects;
using codecrafters_git.Git.Objects.Blobs;
using codecrafters_git.Git.Objects.Commits;
using codecrafters_git.Git.Objects.Trees;
using codecrafters_git.Git.Packfiles;

if (args.Length < 1)
{
    Console.WriteLine("Please provide a command.");
    return;
}

string command = args[0];
string? commandArg = args.Length > 1 ? args[1] : null;

if (command == "init")
{
    InitializeGitDirectory(Directory.GetCurrentDirectory());
    Console.WriteLine("Initialized git directory");
}
else if (command == "cat-file" && commandArg == "-p")
{
    var hash = args[2];
    var gitBlobObject = GitBlobObject.FromHashHexString(hash);

    Console.Write(gitBlobObject.Content);
}
else if (command == "hash-object" && commandArg == "-w")
{
    var gitBlobObject = WriteBlobObject(args[2]);

    Console.Write(gitBlobObject.HashHexString);
}
else if (command == "ls-tree" && commandArg == "--name-only")
{
    var hash = args[2];
    var gitTreeObject = GitTreeObject.FromHashHexString(hash);

    foreach (var name in gitTreeObject.Entries.Select(x => x.Name))
        Console.WriteLine(name);
}
else if (command == "write-tree")
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var gitTreeObject = WriteTreeObject(currentDirectory);

    Console.WriteLine(gitTreeObject.HashHexString);
}
else if (command == "commit-tree")
{
    var treeHash = args[1];
    var parentCommitHash = args[3];
    var commitMessage = args[5];

    var gitCommitObject = WriteGitCommitObject(treeHash, parentCommitHash, commitMessage);

    Console.WriteLine(gitCommitObject.HashHexString);
}
else if (command == "clone")
{
    var repoUrl = args[1];
    var directory = args[2];

    if (!Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    InitializeGitDirectory(directory);

    Directory.SetCurrentDirectory(directory);

    var httpClient = new HttpClient();

    var serviceName = "git-upload-pack";

    var referenceHash = await DiscoverReferenceHash(httpClient, repoUrl, serviceName);

    var gitUploadPackUrl = $"{repoUrl}/{serviceName}";
    var gitUploadPackRequest = new HttpRequestMessage(HttpMethod.Post, gitUploadPackUrl);

    var content = $"0032want {referenceHash}\n00000009done\n";

    gitUploadPackRequest.Content = new StringContent(content, Encoding.ASCII, "application/x-git-upload-pack-request");
    gitUploadPackRequest.Content.Headers.ContentLength = Encoding.ASCII.GetByteCount(content);

    var gitUploadPackResponse = await httpClient.SendAsync(gitUploadPackRequest);

    if (!gitUploadPackResponse.IsSuccessStatusCode)
        Console.WriteLine($"Request failed with status code: {gitUploadPackResponse.StatusCode}");

    var packBytes = await gitUploadPackResponse.Content.ReadAsByteArrayAsync();

    var gitPackfile = new Packfile(packBytes);

    var gitPackObjects = gitPackfile.Decompress();

    foreach (PackObject packObject in gitPackObjects.Where(x => x is UnDeltifiedPackObject))
    {
        var (objectType, bytes) = (UnDeltifiedPackObject)packObject;

        GitObject gitObject = new GitObject(objectType, bytes);
        gitObject.Write();
    }

    foreach (PackObject packObject in gitPackObjects.Where(x => x is DeltifiedPackObject))
    {
        var (baseHash, size, deltaInstructions) = (DeltifiedPackObject)packObject;

        var (baseType, bytes) = GitObject.FromHashHexString(baseHash);

        var completeObjectData = new byte[size];
        using MemoryStream memoryStream = new(completeObjectData);

        foreach (var instruction in deltaInstructions)
        {
            if (instruction is CopyDeltaInstruction copy)
            {
                memoryStream.Write(bytes, copy.Offset, copy.Size);
            }
            else if (instruction is InsertDeltaInstruction insert)
            {
                memoryStream.Write(insert.Data);
            }
            else
            {
                throw new NotSupportedException($"Not supported instruction: {instruction}");
            }
        }

        GitObject gitObject = new GitObject(baseType, completeObjectData);
        gitObject.Write();
    }

    var referenceCommit = GitCommitObject.FromHashHexString(referenceHash);

    var referenceTree = GitTreeObject.FromHashHexString(referenceCommit.TreeHashHexString);

    Checkout(referenceTree, string.Empty);
}
else
{
    throw new ArgumentException($"Unknown command {command}");
}

void Checkout(GitTreeObject tree, string root)
{
    foreach (var entry in tree.Entries)
    {
        switch (entry.Mode)
        {
            case GitTreeObjectEntryMode.RegularFile:
                GitBlobObject blob = GitBlobObject.FromHashHexString(entry.HashHexString);
                var path = Path.Combine(root, entry.Name);
                File.WriteAllText(path, blob.Content);
                break;
            case GitTreeObjectEntryMode.Directory:
                GitTreeObject subTree = GitTreeObject.FromHashHexString(entry.HashHexString);
                var subRootPath = Path.Combine(root, entry.Name);

                Directory.CreateDirectory(subRootPath);

                Checkout(subTree, subRootPath);
                break;
        }
    }
}

async Task<string> DiscoverReferenceHash(HttpClient httpClient, string repoUrl, string serviceName)
{
    var discoveryUrl = $"{repoUrl}/info/refs?service={serviceName}";

    var discoveryResponse = await httpClient.GetAsync(discoveryUrl);
    var discoveryContent = await discoveryResponse.Content.ReadAsStringAsync();

    /*
     sample reference discovery response:
001e# service=git-upload-pack
{0000}{0155}23f0bc3b5c7c3108e41c448f01a3db31e7064bbb HEAD{features}
{003f}23f0bc3b5c7c3108e41c448f01a3db31e7064bbb refs/heads/master
0000
  */

    const string mode = "0155";
    const string flush = "0000";

    var head = discoveryContent.Split(serviceName)[1].Split("HEAD")[0].Trim();
    return head[(flush.Length + mode.Length)..];
}

GitTreeObject WriteTreeObject(string directory)
{
    var directories = Directory.GetDirectories(directory).Where(x => !x.EndsWith(".git"));
    var files = Directory.GetFiles(directory);
    var directoryAndFiles = directories.Concat(files).OrderBy(x => x);

    var treeObject = new GitTreeObject();
    foreach (var directoryAndFile in directoryAndFiles)
    {
        if (Directory.Exists(directoryAndFile))
        {
            var gitTreeObject = WriteTreeObject(directoryAndFile);
            var gitTreeObjectEntry = new GitTreeObjectEntry(GitTreeObjectEntryMode.Directory, Path.GetFileName(directoryAndFile), gitTreeObject.Hash);
            treeObject.Entries.Add(gitTreeObjectEntry);
        }
        else
        {
            var gitBlobObject = WriteBlobObject(directoryAndFile);
            var gitObjectTreeEntry = new GitTreeObjectEntry(GitTreeObjectEntryMode.RegularFile, Path.GetFileName(directoryAndFile), gitBlobObject.Hash);
            treeObject.Entries.Add(gitObjectTreeEntry);
        }
    }

    Directory.CreateDirectory(Path.GetDirectoryName(treeObject.Path)!);
    File.WriteAllBytes(treeObject.Path, treeObject.Bytes.Compress());

    return treeObject;
}

GitBlobObject WriteBlobObject(string filePath)
{
    var fileContent = File.ReadAllText(filePath);
    var blobObject = GitBlobObject.FromContent(fileContent);

    Directory.CreateDirectory(Path.GetDirectoryName(blobObject.Path)!);
    File.WriteAllBytes(blobObject.Path, blobObject.Bytes.Compress());

    return blobObject;
}

GitCommitObject WriteGitCommitObject(string treeHash, string parentCommitHash, string commitMessage)
{
    var gitCommitObjectAuthorEntry = new GitCommitObjectAuthorEntry("burhansavci", "burhansavci@gmail.com", DateTimeOffset.UtcNow);
    var gitCommitObjectCommiterEntry = new GitCommitObjectComitterEntry("burhansavci", "burhansavci@gmail.com", DateTimeOffset.UtcNow);

    var gitCommitObject = new GitCommitObject(treeHash, parentCommitHash, commitMessage, gitCommitObjectAuthorEntry, gitCommitObjectCommiterEntry);

    Directory.CreateDirectory(Path.GetDirectoryName(gitCommitObject.Path)!);
    File.WriteAllBytes(gitCommitObject.Path, gitCommitObject.Bytes.Compress());

    return gitCommitObject;
}

void InitializeGitDirectory(string directoryPath)
{
    Directory.CreateDirectory(Path.Combine(directoryPath, ".git"));
    Directory.CreateDirectory(Path.Combine(directoryPath, ".git", "objects"));
    Directory.CreateDirectory(Path.Combine(directoryPath, ".git", "refs"));
    File.WriteAllText(Path.Combine(directoryPath, ".git", "HEAD"), "ref: refs/heads/main\n");
}