namespace codecrafters_git.Git.Objects.Commits;

public record GitCommitObjectAuthorEntry(string Name = "burhansavci", string Email = "burhansavci@gmail.com")
{
    public DateTimeOffset Date { get; } = DateTimeOffset.Now;
    public string DateInSeconds => Date.ToUnixTimeSeconds().ToString();
    public string DateTimeZone => Date.ToString("zzz");

    public override string ToString() => $"author {Name} <{Email}> {DateInSeconds} {DateTimeZone}";
}