namespace UserTrace.Models;

/// <summary>
/// Dados estruturados de um usuário do Active Directory.
/// </summary>
public sealed class UserInfo
{
    // Identidade
    public string SamAccountName { get; init; } = string.Empty;
    public string FullName        { get; init; } = string.Empty;
    public string Email           { get; init; } = string.Empty;
    public string PhoneNumber     { get; init; } = string.Empty; // AD: telephoneNumber (ramal/telefone)
    public string Office          { get; init; } = string.Empty; // AD: physicalDeliveryOfficeName (setor/local)
    public string OrganizationalUnit { get; init; } = string.Empty; // AD: derived from distinguishedName (first OU after CN)
    public string Comment         { get; init; } = string.Empty;
    public string UserComment     { get; init; } = string.Empty;

    // Status da conta
    public bool   AccountActive   { get; init; }
    public string AccountExpires  { get; init; } = string.Empty;

    // Senha
    public string PasswordLastSet    { get; init; } = string.Empty;
    public string PasswordExpiresOn  { get; init; } = string.Empty;
    public string PasswordDaysToExpire { get; init; } = string.Empty;
    public string BadPasswordCount   { get; init; } = string.Empty;
    public string BadPasswordTime    { get; init; } = string.Empty;
    public string LockoutTime        { get; init; } = string.Empty;
    public bool   PasswordNeverExpires { get; init; }
    public bool   PasswordExpired    { get; init; }
    public bool   PasswordRequired   { get; init; }
    public bool   PasswordChangeable { get; init; }
    public bool   SmartcardRequired  { get; init; }

    // Logon
    public string LastLogon      { get; init; } = string.Empty;
    public string LastLogoff     { get; init; } = string.Empty;
    public string Workstations   { get; init; } = string.Empty;
    public string LogonScript    { get; init; } = string.Empty;
    public string ProfilePath    { get; init; } = string.Empty;
    public string HomeDirectory  { get; init; } = string.Empty;

    // Grupos
    public IReadOnlyList<string> LocalGroups  { get; init; } = [];
    public IReadOnlyList<string> GlobalGroups { get; init; } = [];

    // Domínio
    public string Domain { get; init; } = string.Empty;
}
