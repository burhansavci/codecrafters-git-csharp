namespace codecrafters_git.Git.Objects.Commits;

public record GitCommitObjectComitterEntry(string Name, string Email, DateTimeOffset Date)
{
    public static GitCommitObjectComitterEntry FromContent(string content)
    {
        var committerEntry = content.Split('\n')[3];
        var committerEntryParts = committerEntry.Split(' ');

        var name = committerEntryParts[1].TrimEnd('<').TrimEnd('>').TrimEnd(' ');
        var email = committerEntryParts[2].TrimEnd('<').TrimEnd('>').TrimEnd(' ');
        var date = GetDate(committerEntryParts);

        return new GitCommitObjectComitterEntry(name, email, date);
    }

    public string DateInSeconds => Date.ToUnixTimeSeconds().ToString();
    public string DateTimeZone => Date.ToString("zzz");

    public override string ToString() => $"committer {Name} <{Email}> {DateInSeconds} {DateTimeZone}";

    private static DateTimeOffset GetDate(string[] entryParts)
    {
        var dateInSeconds = entryParts[3];
        var dateTimeZone = entryParts[4];

        var hours = int.Parse(dateTimeZone[..3]);
        var minutes = int.Parse(dateTimeZone.Substring(3, 2));
        var offset = new TimeSpan(hours, minutes, 0);

        return DateTimeOffset.FromUnixTimeSeconds(long.Parse(dateInSeconds)).ToOffset(offset);
    }
}