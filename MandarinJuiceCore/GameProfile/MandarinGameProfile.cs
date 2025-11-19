using MandarinJuiceCore.GamingPlatforms;
using Mi5hmasH.GameProfile;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using MandarinJuiceCore.Models.DSSS.Mandarin;

namespace MandarinJuiceCore.GameProfile;

public class MandarinGameProfile : IEquatable<MandarinGameProfile>, INotifyPropertyChanged, IGameProfile
{
    /// <summary>
    /// Gets or sets metadata information related to the game profile.
    /// </summary>
    public GameProfileMeta Meta { get; set; } = new("MandarinDSSS", new Version(1, 0, 0, 0));

    /// <summary>
    /// Gets or sets the title of a game.
    /// </summary>
    public string? GameTitle
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(GameTitle));
        }
    }

    /// <summary>
    /// Gets or sets the GamingPlatform.
    /// </summary>
    public GamingPlatform Platform
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(Platform));
        }
    } = GamingPlatform.Other;

    /// <summary>
    /// Gets or sets the application identifier associated with this instance.
    /// </summary>
    public string? AppId
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(AppId));
        }
    }

    /// <summary>
    /// Gets or sets the Game Profile Icon encoded with Base64.
    /// </summary>
    public string? Base64GpIcon
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(Base64GpIcon));
        }
    }

    /// <summary>
    /// Gets or sets the Mandarin seed.
    /// </summary>
    public ulong MandarinSeed
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(MandarinSeed));
        }
    } = 0;

    /// <summary>
    /// Gets or sets the Parsing Method Variant.
    /// </summary>
    public uint ParseVariant
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(ParseVariant));
        }
    } = 0;

    /// <summary>
    /// Gets or sets the Mandarin File Flavor.
    /// </summary>
    public MandarinFileFlavor MandarinFileFlavor
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(MandarinFileFlavor));
        }
    } = MandarinFileFlavor.Default;

    /// <summary>
    /// Copies the game profile data from the specified object if it is an instance of MandarinGameProfile.
    /// </summary>
    /// <param name="other">The object from which to copy game profile data.</param>
    public void Set(object other)
    {
        if (other is not MandarinGameProfile profile) return;
        GameTitle = profile.GameTitle;
        AppId = profile.AppId;
        Base64GpIcon = profile.Base64GpIcon;
        Platform = profile.Platform;
        MandarinSeed = profile.MandarinSeed;
        ParseVariant = profile.ParseVariant;
        MandarinFileFlavor = profile.MandarinFileFlavor;
    }

    public bool Equals(MandarinGameProfile? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        var sc = StringComparer.Ordinal;
        return Meta.Equals(other.Meta) &&
               sc.Equals(GameTitle, other.GameTitle) &&
               sc.Equals(AppId, other.AppId) &&
               sc.Equals(Base64GpIcon, other.Base64GpIcon) &&
               Platform == other.Platform &&
               MandarinSeed == other.MandarinSeed &&
               ParseVariant == other.ParseVariant &&
               MandarinFileFlavor == other.MandarinFileFlavor;
    }

    public int GetHashCodeStable()
    {
        var hc = new HashCode();
        var sc = StringComparer.Ordinal;
        // Add fields to the hash code computation
        hc.Add(Meta);
        hc.Add(GameTitle, sc);
        hc.Add(AppId, sc);
        hc.Add(Base64GpIcon, sc);
        hc.Add(Platform);
        hc.Add(MandarinSeed);
        hc.Add(ParseVariant);
        hc.Add(MandarinFileFlavor);
        return hc.ToHashCode();
    }

    // This is a workaround to avoid the default GetHashCode() implementation in objects where all fields are mutable.
    private readonly Guid _uniqueId = Guid.NewGuid();
    
    public override int GetHashCode()
        => _uniqueId.GetHashCode();

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is MandarinGameProfile castedObj && Equals(castedObj);

    public static bool operator ==(MandarinGameProfile? left, MandarinGameProfile? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(MandarinGameProfile? left, MandarinGameProfile? right) 
        => !(left == right);

    // MVVM support
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}