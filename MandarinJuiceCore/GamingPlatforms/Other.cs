namespace MandarinJuiceCore.GamingPlatforms;

public class Other : IGamingPlatform
{
    public string AppId { get; set; } = string.Empty;
    public string UserIdInput { get; set; } = string.Empty;
    public string UserIdOutput { get; set; } = string.Empty;
    public uint ParseVariant { get; set; } = 0;

    public void OpenStoreProductPage()
    {
        // do nothing;
    }

    public ulong GetParsedUserIdInput() => Convert.ToUInt64(UserIdInput);
    public ulong GetParsedUserIdOutput() => Convert.ToUInt64(UserIdOutput);
}