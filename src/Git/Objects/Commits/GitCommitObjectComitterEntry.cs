namespace codecrafters_git.Git.Objects.Commits;

public record GitCommitObjectComitterEntry(string Name, string Email, DateTimeOffset Date)
{
    private const string CommitterEntryPrefix = "committer "; 
    
    public static GitCommitObjectComitterEntry FromContent(string content)
    {
        var committerEntry = content.Split('\n').First(entry => entry.Trim().StartsWith(CommitterEntryPrefix));
        
        int startIndexOfEmail = committerEntry.IndexOf('<');
        int endIndexOfEmail = committerEntry.IndexOf('>');
        
        var name = committerEntry.Substring(CommitterEntryPrefix.Length, startIndexOfEmail - CommitterEntryPrefix.Length).Trim();
        var email = committerEntry.Substring(startIndexOfEmail + 1, endIndexOfEmail - startIndexOfEmail - 1);
        var dateParts = committerEntry.Substring(endIndexOfEmail + 1, committerEntry.Length - endIndexOfEmail - 1).Trim().Split(' ');

        var date = GetDate(dateParts[0], dateParts[1]);

        return new GitCommitObjectComitterEntry(name, email, date);
    }

    public string DateInSeconds => Date.ToUnixTimeSeconds().ToString();
    public string DateTimeZone => Date.ToString("zzz");

    public override string ToString() => $"committer {Name} <{Email}> {DateInSeconds} {DateTimeZone}";

    private static DateTimeOffset GetDate(string dateInSeconds, string dateTimeZone)
    {
        var hours = int.Parse(dateTimeZone[..3]);
        var minutes = int.Parse(dateTimeZone.Substring(3, 2));
        var offset = new TimeSpan(hours, minutes, 0);

        return DateTimeOffset.FromUnixTimeSeconds(long.Parse(dateInSeconds)).ToOffset(offset);
    }
}