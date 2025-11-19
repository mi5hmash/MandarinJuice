namespace MandarinJuiceCore.Infrastructure;

/// <summary>
/// Provides constant values used as cryptographic keys or identifiers within the application.
/// </summary>
/// <remarks>
/// If a program can decrypt something, then the user can as well because the program runs on their machine.
/// </remarks>
public static class Keychain
{
    public const string GpMagic = "vZepCnA+/ZmBNcJIr8eGkiT/unH3UArgXCN9KIw/PF4=";
    public const string SettingsMagic = "o7/ZsQIY6KPBCT7wxXY0NRvd55cX1yRalQIKzWgWsko=";
}