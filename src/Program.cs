using System.Net.Http.Headers;
using System.Text;
using codecrafters_git.Git.Extensions;
using codecrafters_git.Git.Objects;
using codecrafters_git.Git.Objects.Blobs;
using codecrafters_git.Git.Objects.Commits;
using codecrafters_git.Git.Objects.Trees;

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
    var url = args[1];
    var directory = args[2];

    if (!Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("git-protocol", "version=1");

    var serviceName = "git-upload-pack";
    var discoveryUrl = $"{url}/info/refs?service={serviceName}";
    Console.WriteLine(url);
    var discoveryResponse = await httpClient.GetAsync(discoveryUrl);
    var discoveryContent = await discoveryResponse.Content.ReadAsStringAsync();

    Console.WriteLine(discoveryContent);
    /**
001e# service=git-upload-pack
0000015523f0bc3b5c7c3108e41c448f01a3db31e7064bbb HEADmulti_ack thin-pack side-band side-band-64k ofs-delta shallow deepen-since deepen-not deepen-relative no-progress include-tag multi_ack_detailed allow-tip-sha1-in-want allow-reachable-sha1-in-want no-done symref=HEAD:refs/heads/master filter object-format=sha1 agent=git/github-d37f7b990c25
003f23f0bc3b5c7c3108e41c448f01a3db31e7064bbb refs/heads/master
0000
      */
    var mode = "0155";
    var flush = "0000";
    var head = discoveryContent.Split(serviceName)[1].Split("HEAD")[0].Trim();
    var firstRefHash = head[(flush.Length + mode.Length)..];

    var gitUploadPackUrl = $"{url}/{serviceName}";
    var gitUploadPackRequest = new HttpRequestMessage(HttpMethod.Post, gitUploadPackUrl);

    var content = $"0032want {firstRefHash}\n00000009done\n";

    gitUploadPackRequest.Content = new StringContent(content, Encoding.ASCII, "application/x-git-upload-pack-request");
    gitUploadPackRequest.Content.Headers.ContentLength = Encoding.ASCII.GetByteCount(content);

    var gitUploadPackResponse = await httpClient.SendAsync(gitUploadPackRequest);

    if (gitUploadPackResponse.IsSuccessStatusCode)
    {
        var gitUploadPackResponseContent = await gitUploadPackResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Git Upload Pack Response ({gitUploadPackResponseContent.Length}):");
        Console.WriteLine(gitUploadPackResponseContent);
    }
    else
    {
        Console.WriteLine($"Request failed with status code: {gitUploadPackResponse.StatusCode}");
    }
}
else
{
    throw new ArgumentException($"Unknown command {command}");
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
    var gitCommitObject = new GitCommitObject(treeHash, parentCommitHash, commitMessage);

    Directory.CreateDirectory(Path.GetDirectoryName(gitCommitObject.Path)!);
    File.WriteAllBytes(gitCommitObject.Path, gitCommitObject.Bytes.Compress());

    return gitCommitObject;
}