namespace UserTrace.Models;

public sealed class CommandResult
{
    public UserInfo? UserInfo  { get; init; }
    public string    Error     { get; init; } = string.Empty;
    public int       ExitCode  { get; init; }
    public bool      Success   => ExitCode == 0;
}
