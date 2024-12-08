namespace codecrafters_git.Git.Objects.Commits;

public record GitCommitObjectAuthorEntry(string Name, string Email, DateTimeOffset Date)
{
    private const string AuthorEntryPrefix = "author "; 
    
    public static GitCommitObjectAuthorEntry FromContent(string content)
    {
        var authorEntry = content.Split('\n').First(entry => entry.Trim().StartsWith(AuthorEntryPrefix));
        
        int startIndexOfEmail = authorEntry.IndexOf('<');
        int endIndexOfEmail = authorEntry.IndexOf('>');

        var name = authorEntry.Substring(AuthorEntryPrefix.Length, startIndexOfEmail - AuthorEntryPrefix.Length).Trim();
        var email = authorEntry.Substring(startIndexOfEmail + 1, endIndexOfEmail - startIndexOfEmail - 1);
        var dateParts = authorEntry.Substring(endIndexOfEmail + 1, authorEntry.Length - endIndexOfEmail - 1).Trim().Split(' '); 
        
        var date = GetDate(dateParts[0], dateParts[1]);
        
        return new GitCommitObjectAuthorEntry(name, email, date);
    }

    public string DateInSeconds => Date.ToUnixTimeSeconds().ToString();
    public string DateTimeZone => Date.ToString("zzz");

    public override string ToString() => $"author {Name} <{Email}> {DateInSeconds} {DateTimeZone}";

    private static DateTimeOffset GetDate(string dateInSeconds, string dateTimeZone)
    {
        var hours = int.Parse(dateTimeZone[..3]);
        var minutes = int.Parse(dateTimeZone.Substring(3, 2));
        var offset = new TimeSpan(hours, minutes, 0);

        return DateTimeOffset.FromUnixTimeSeconds(long.Parse(dateInSeconds)).ToOffset(offset);
    }
}