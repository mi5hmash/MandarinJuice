using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mi5hmasH.AppInfo;
using Mi5hmasH.GameProfile;
using Microsoft.Win32;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MandarinJuiceCore.GameProfile;
using MandarinJuiceCore.GamingPlatforms;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using MandarinJuiceCore.Models.DSSS.Mandarin;
using MandarinJuiceWpf.Fonts;

namespace GameProfileEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    #region APP_INFO
    public readonly MyAppInfo AppInfo = new("GameProfile Editor");
    public string AppTitle => AppInfo.Name;
    public static string AppAuthor => MyAppInfo.Author;
    public static string AppVersion => $"v{MyAppInfo.Version}";

    [RelayCommand] private static void VisitAuthorsGithub() => Urls.OpenAuthorsGithub();
    [RelayCommand] private static void VisitProjectsRepo() => Urls.OpenProjectsRepo();
    #endregion

    #region ICONS
    public static string ExportIcon => IconFont.Export;
    public static string GithubIcon => IconFont.Github;
    public static string ImportIcon => IconFont.Import;
    #endregion

    #region PROGRESS_REPORTER
    [ObservableProperty] private string _progressText = "Loading...";
    private readonly ProgressReporter _progressReporter;
    #endregion

    #region GAME_PROFILE
    [ObservableProperty] private GameProfileManager<MandarinGameProfile> _gameProfileManager = new();
    [ObservableProperty] private MandarinFileFlavor[] _mandarinFileFlavors = Enum.GetValues<MandarinFileFlavor>();

    private const string GameProfileExtension = ".bin";

    private void InitializeGameProfileManager()
        => GameProfileManager.SetEncryptor(Keychain.GpMagic);

    private const string FileDialogFilter = $"Game Profile (*{GameProfileExtension})|*{GameProfileExtension}";
    private const string FileDialogFilterImage = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";

    private void LoadGameProfileIcon(string path)
    {
        var imageBytes = File.ReadAllBytes(path);
        var base64Img = Convert.ToBase64String(imageBytes);
        GameProfileManager.GameProfile.Base64GpIcon = base64Img;
        _progressReporter.Report("GameProfile Icon imported.");
    }

    private void SaveGameProfileIcon(string path)
    {
        var imageBytes = Convert.FromBase64String(GameProfileManager.GameProfile.Base64GpIcon ?? string.Empty);
        File.WriteAllBytes(path, imageBytes);
        _progressReporter.Report("GameProfile Icon exported.");
    }

    private void LoadGameProfileFile(string path)
    {
        var result = "GameProfile loaded.";
        try { GameProfileManager.Load(path); }
        catch { result = "Invalid GameProfile."; }
        finally { _progressReporter.Report(result); }
    }

    [RelayCommand]
    private void NewGameProfile()
    {
        GameProfileManager.NewProfile();
        _progressReporter.Report("New GameProfile created.");
    }

    [RelayCommand] 
    private void LoadGameProfile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = FileDialogFilter,
            InitialDirectory = GameProfileManager.GetDirectory()
        };
        if (openFileDialog.ShowDialog() != true) return;
        LoadGameProfileFile(openFileDialog.FileName);
    }

    [RelayCommand]
    private void ImportGameProfileImage()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = FileDialogFilterImage
        };
        if (openFileDialog.ShowDialog() != true) return;
        LoadGameProfileIcon(openFileDialog.FileName);
    }

    [RelayCommand]
    private void ExportGameProfileImage()
    {
        SaveFileDialog saveFileDialog = new()
        {
            FileName = "gpicon.png",
            Filter = FileDialogFilterImage
        };
        if (saveFileDialog.ShowDialog() != true) return;
        SaveGameProfileIcon(saveFileDialog.FileName);
    }

    [RelayCommand] 
    private void SaveGameProfile()
    {
        UpdateActiveTextBoxBinding();
        GameProfileManager.Save();
        _progressReporter.Report("GameProfile saved.");
    }

    [RelayCommand] 
    private void SaveGameProfileAs()
    {
        UpdateActiveTextBoxBinding();
        SaveFileDialog saveFileDialog = new()
        {
            FileName = GameProfileManager.CurrentlyLoadedProfileName ?? GameProfileManager<MandarinGameProfile>.NewProfileName,
            Filter = FileDialogFilter
        };
        if (saveFileDialog.ShowDialog() != true) return;
        GameProfileManager.Save(saveFileDialog.FileName);
        _progressReporter.Report("GameProfile saved as new file.");
    }
    #endregion

    #region FILE_DROP
    public void OnFileDrop(string operationType, StringCollection filePaths)
    {
        if (filePaths.Count < 1) return;
        var path = filePaths[0] ?? string.Empty;
        if (operationType != "FileDropped") return;
        var extension = Path.GetExtension(path);
        switch (extension)
        {
            case ".bin":
                LoadGameProfileFile(path);
                break;
            case ".png" or ".jpg" or ".jpeg" or ".bmp":
                LoadGameProfileIcon(path);
                break;
        }
    }
    #endregion

    #region GAMING_PLATFORM
    [ObservableProperty] private GamingPlatform[] _gamingPlatformOptions = Enum.GetValues<GamingPlatform>();
    #endregion

    public MainWindowViewModel()
    {
        // Initialize GameProfile Manager
        InitializeGameProfileManager();
        // Initialize ProgressReporter
        _progressReporter = new ProgressReporter(new Progress<string>(s => ProgressText = s));
        _progressReporter.Report("Ready");
    }

    #region ACTIONS

    /// <summary>
    /// Updates the source of the active TextBox's text binding, if a TextBox currently has keyboard focus. This is useful when application has a keyboard shortcuts set.
    /// </summary>
    private static void UpdateActiveTextBoxBinding()
    {
        try
        {
            if (Keyboard.FocusedElement is not TextBox focusedElement) return;
            var binding = BindingOperations.GetBindingExpression(focusedElement, TextBox.TextProperty);
            binding?.UpdateSource();
        }
        catch
        {
            // ignored
        }
    }

    [RelayCommand] private static void ExitApplication() => Application.Current.Shutdown();
    #endregion
}