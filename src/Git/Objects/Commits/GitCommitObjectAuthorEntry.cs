namespace codecrafters_git.Git.Objects.Commits;

public record GitCommitObjectAuthorEntry(string Name, string Email, DateTimeOffset Date)
{
    public static GitCommitObjectAuthorEntry FromContent(string content)
    {
        var authorEntry = content.Split('\n')[2];
        var authorEntryParts = authorEntry.Split(' ');
        
        var name = authorEntryParts[1].TrimEnd('<').TrimEnd('>').TrimEnd(' ');
        var email = authorEntryParts[2].TrimEnd('<').TrimEnd('>').TrimEnd(' ');
        var date = GetDate(authorEntryParts);

        return new GitCommitObjectAuthorEntry(name, email, date);
    }

    public string DateInSeconds => Date.ToUnixTimeSeconds().ToString();
    public string DateTimeZone => Date.ToString("zzz");

    public override string ToString() => $"author {Name} <{Email}> {DateInSeconds} {DateTimeZone}";

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