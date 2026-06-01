namespace MandarinJuiceCore.GamingPlatformsFactory.Platforms;

public abstract class Default : IGamingPlatform
{
    public virtual string AppId { get; set; } = string.Empty;
    public virtual string UserIdInput { get; set; } = string.Empty;
    public virtual string UserIdOutput { get; set; } = string.Empty;
    public virtual uint ParseVariant { get; set; }

    public abstract void OpenStoreProductPage();

    public virtual ulong GetParsedUserIdInput() => ParseUserId(UserIdInput);
    public virtual ulong GetParsedUserIdOutput() => ParseUserId(UserIdOutput);
    
    public virtual ulong ParseUserId(string userId) => Convert.ToUInt64(userId);
    public virtual ulong ParseUserId(uint userId) => userId;
    public virtual ulong ParseUserId(ulong userId) => userId;
}