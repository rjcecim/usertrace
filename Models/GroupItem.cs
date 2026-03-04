namespace UserTrace.Models;

/// <summary>
/// Representa um grupo do Active Directory na lista de grupos.
/// </summary>
public sealed class GroupItem
{
    public string Name        { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string GroupType   { get; init; } = string.Empty; // "Global", "Local", "Universal"

    public override string ToString() => Name;
}
