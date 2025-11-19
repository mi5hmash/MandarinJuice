using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MandarinJuiceWpf.Fonts;
using MandarinJuiceWpf.Settings;
using Mi5hmasH.AppInfo;
using Mi5hmasH.AppSettings;
using Mi5hmasH.AppSettings.Flavors;
using Mi5hmasH.GameProfile;
using Mi5hmasH.Logger;
using Mi5hmasH.Logger.Models;
using Mi5hmasH.Logger.Providers;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Media;
using System.Windows;
using MandarinJuiceCore;
using MandarinJuiceCore.GameProfile;
using MandarinJuiceCore.GamingPlatforms;
using MandarinJuiceCore.Helpers;
using MandarinJuiceCore.Infrastructure;
using MandarinJuiceWpf.Helpers;

namespace MandarinJuiceWpf.ViewModels;

public partial class MainWindowViewModel : ObservableValidator
{
    #region APP_INFO
    public readonly MyAppInfo AppInfo = new("MandarinJuice");
    public string AppTitle => AppInfo.Name;
    public static string AppAuthor => MyAppInfo.Author;
    public static string AppVersion => $"v{MyAppInfo.Version}";

    [RelayCommand] private static void VisitAuthorsGithub() => Urls.OpenAuthorsGithub();
    [RelayCommand] private static void VisitProjectsRepo() => Urls.OpenProjectsRepo();
    #endregion

    #region ICONS
    public static string DecryptIcon => IconFont.Decrypt;
    public static string EncryptIcon => IconFont.Encrypt;
    public static string FolderIcon => IconFont.Folder;
    public static string FolderSymlinkIcon => IconFont.FolderSymlink;
    public static string GithubIcon => IconFont.Github;
    public static string InterchangeIcon => IconFont.Interchange;
    public static string ResignIcon => IconFont.Resign;
    public static string XCircleIcon => IconFont.XCircle;
    #endregion

    #region UI_STATE
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAbortAllowed;
    #endregion

    #region PROGRESS_REPORTER
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _progressText = "Loading...";
    private readonly ProgressReporter _progressReporter;
    #endregion

    #region LOGGER
    private readonly SimpleLogger _logger;
    private void InitializeLogger()
    {
        // Configure StatusBarLogProvider
        var statusBarLogProvider = new StatusBarLogProvider(_progressReporter.Report);
        _logger.AddProvider(statusBarLogProvider);
        // Configure FileLogProvider
        var fileLogProvider = new FileLogProvider(MyAppInfo.RootPath, 2);
        fileLogProvider.CreateLogFile();
        _logger.AddProvider(fileLogProvider);
        // Add event handler for unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not Exception exception) return;
            var logEntry = new LogEntry(SimpleLogger.LogSeverity.Critical, $"Unhandled Exception: {exception}");
            fileLogProvider.Log(logEntry);
            fileLogProvider.Flush();
        };
        // Flush log providers on process exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _logger.Flush();
    }
    #endregion

    #region USER_ID
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression("^[0-9A-Fa-f]+$", ErrorMessage = "Only hexadecimal characters are allowed.")]
    [Required]
    private string _userIdInput = "0";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [RegularExpression("^[0-9A-Fa-f]+$", ErrorMessage = "Only hexadecimal characters are allowed.")]
    [Required]
    private string _userIdOutput = "0";

    [RelayCommand]
    private void SwapUserIds()
    {
        (UserIdInput, UserIdOutput) = (UserIdOutput, UserIdInput);
        _progressReporter.Report("User IDs have been swapped.");
    }
    #endregion

    #region INPUT_FOLDER_PATH
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    private string _inputFolderPath = MyAppInfo.RootPath;
    
    partial void OnInputFolderPathChanged(string value)
    {
        if (Directory.Exists(value)) return;
        if (File.Exists(value))
        {
            _inputFolderPath = Path.GetDirectoryName(value) ?? string.Empty;
            _progressReporter.Report("Input Folder Path is valid.");
            return;
        }
        _progressReporter.Report("Invalid Input Folder Path!");
        _inputFolderPath = string.Empty;
    }

    [RelayCommand]
    private void SelectInputFolderPath()
    {
        OpenFileDialog openFileDialog = new()
        {
            InitialDirectory = InputFolderPath,
            Filter = "All Files (*.*)|*.*"
        };
        if (openFileDialog.ShowDialog() == true) InputFolderPath = openFileDialog.FileName;
    }
    #endregion

    #region OUTPUT_FOLDER_PATH
    [RelayCommand]
    private static void OpenOutputDirectory()
        => Directories.OpenDirectory(Directories.Output);
    #endregion

    #region APP_SETTINGS
    private readonly AppSettingsManager<MyAppSettings, Json> _appSettingsManager;
    private void InitializeAppSettings()
    {
        _appSettingsManager.SetEncryptor(Keychain.SettingsMagic);
        try { _appSettingsManager.Load(); }
        catch {
            // ignore
        }
        // Apply loaded settings
        LoadAppSettings();
        // Save settings on exit
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SaveAppSettings();
    }
    private void LoadAppSettings()
    {
        UserIdInput = _appSettingsManager.Settings.UserIdInput;
        UserIdOutput = _appSettingsManager.Settings.UserIdOutput;
        SuperUserManager.IsSuperUser = _appSettingsManager.Settings.IsSu;
    }
    private void SaveAppSettings()
    {
        _appSettingsManager.Settings.UserIdInput = UserIdInput;
        _appSettingsManager.Settings.UserIdOutput = UserIdOutput;
        _appSettingsManager.Settings.IsSu = SuperUserManager.IsSuperUser;
        _appSettingsManager.Save();
    }
    #endregion

    #region GAME_PROFILE
    // GameProfile Manager
    [ObservableProperty] private GameProfileManager<MandarinGameProfile> _gameProfileManager = new();
    
    private const string GameProfileExtension = ".bin";

    [ObservableProperty] private string? _gameProfileAppId;
    [ObservableProperty] private IGamingPlatform _gamingPlatform = new Other();

    private void LoadGameProfileFile(string path)
    {
        var result = "GameProfile loaded.";
        try { GameProfileManager.Load(path); }
        catch { result = "Invalid GameProfile."; }
        finally { 
            _progressReporter.Report(result);
            var appId = GameProfileManager.GameProfile.AppId ?? "0";
            GameProfileAppId = $"App ID: {appId}";
            // Copy MandarinFileFlavor to Core
            _core.MandarinFileFlavor = GameProfileManager.GameProfile.MandarinFileFlavor;
            // Copy Mandarin Seed to Deencryptor
            _core.Deencryptor.MandarinSeed = GameProfileManager.GameProfile.MandarinSeed;
            // Set GamingPlatform
            GamingPlatform = GamingPlatformHelper.GetGamingPlatform(GameProfileManager.GameProfile.Platform);
            GamingPlatform.AppId = appId;
            GamingPlatform.ParseVariant = GameProfileManager.GameProfile.ParseVariant;
        }
    }
    
    [ObservableProperty] private ObservableCollection<string> _gameProfileFiles = [];
    [ObservableProperty] private string? _selectedGameProfileFile;

    partial void OnSelectedGameProfileFileChanged(string? value) 
        => LoadGameProfileFile(value ?? string.Empty);

    private void InitializeGameProfileManager()
    {
        GameProfileManager.SetEncryptor(Keychain.GpMagic);
        InitializeGameProfileFileWatcher();
    }

    // FileSystemWatcher for GameProfile directory
    private FileSystemWatcher? _gpWatcher;
    private void InitializeGameProfileFileWatcher()
    {
        try
        {
            Directory.CreateDirectory(Directories.Profiles);

            // Load initial state
            RefreshGpFiles();

            // Setup watcher
            _gpWatcher = new FileSystemWatcher(Directories.Profiles, $"*{GameProfileExtension}")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            // Use lightweight handlers that refresh the list on any relevant change
            _gpWatcher.Created += (_, _) => RefreshGpFilesAsync();
            _gpWatcher.Deleted += (_, _) => RefreshGpFilesAsync();
            _gpWatcher.Renamed += (_, _) => RefreshGpFilesAsync();
            _gpWatcher.Changed += (_, _) => RefreshGpFilesAsync();

            // Ensure disposal on process exit
            AppDomain.CurrentDomain.ProcessExit += (_, _) => _gpWatcher?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to initialize GP Watcher: {ex.Message}");
        }
    }
    private void RefreshGpFiles()
    {
        if (!Directory.Exists(Directories.Profiles))
        {
            Application.Current?.Dispatcher.Invoke(() => GameProfileFiles.Clear());
            _progressReporter.Report("GameProfile File list has been cleared.");
            return;
        }

        var files = Directory.EnumerateFiles(Directories.Profiles, $"*{GameProfileExtension}", SearchOption.TopDirectoryOnly)
                             .OrderBy(Path.GetFileName)
                             .ToList();

        Application.Current?.Dispatcher.Invoke(() =>
        {
            GameProfileFiles.Clear();
            foreach (var f in files) GameProfileFiles.Add(f);
            _progressReporter.Report("GameProfile File list has been refreshed.");
        });
    }
    private void RefreshGpFilesAsync() => Task.Run(RefreshGpFiles);
    
    [RelayCommand]
    private static void OpenProfilesDirectory()
        => Directories.OpenDirectory(Directories.Profiles);

    [RelayCommand]
    private void VisitStoreProductPage()
        => GamingPlatform.OpenStoreProductPage();
    #endregion

    #region FILE_DROP
    public void OnFileDrop(string operationType, StringCollection filePaths)
    {
        if (filePaths.Count < 1) return;
        if (operationType == "GetInputPath") InputFolderPath = filePaths[0] ?? string.Empty;
    }
    #endregion

    private CancellationTokenSource _cts = new();
    private readonly Core _core;
    [ObservableProperty] private SuperUserManager _superUserManager;
    
    public MainWindowViewModel()
    {
        // Initialize ProgressReporter
        _progressReporter = new ProgressReporter(
            new Progress<string>(s => ProgressText = s),
            new Progress<int>(i => ProgressValue = i)
        );
        // Initialize Logger
        _logger = new SimpleLogger
        {
            LoggedAppName = AppInfo.Name
        };
        InitializeLogger();
        // Initialize Core
        _core = new Core(_logger, _progressReporter);
        // Initialize SuperUserManager
        SuperUserManager = new SuperUserManager(_progressReporter);
        // Initialize AppSettings
        _appSettingsManager = new AppSettingsManager<MyAppSettings, Json>(null, MyAppInfo.RootPath);
        InitializeAppSettings();
        // Initialize GameProfile Manager
        InitializeGameProfileManager();
        // Finalize setup
        _progressReporter.Report("Ready", 100);
    }

    #region ACTIONS

    public bool CanSubmit => SelectedGameProfileFile != null && !HasErrors;

    [RelayCommand]
    public void AbortAction()
    {
        if (!IsAbortAllowed || !IsBusy) return;
        _cts.Cancel();
    }

    private async Task PerformAction(Func<Task> function, bool canBeAborted = false)
    {
        if (IsBusy) return;
        if (!CanSubmit) return;
        IsBusy = true;
        if (canBeAborted) IsAbortAllowed = true;
        try
        {
            await function();
        }
        finally
        {
            // play sound
            if (_cts.IsCancellationRequested)
                SystemSounds.Beep.Play();
            else
            {
                using var sp = new SoundPlayer(Properties.Resources.typewriter_machine);
                sp.Play();
            }
            // reset flags
            if (canBeAborted) IsAbortAllowed = false;
            IsBusy = false;

            // flush logs
            await _logger.FlushAsync();
        }
    }
    
    [RelayCommand]
    private async Task DecryptAllAsync()
    {
        _cts = new CancellationTokenSource();
        GamingPlatform.UserIdInput = UserIdInput;
        await PerformAction(() => _core.DecryptFilesAsync(InputFolderPath, GamingPlatform, _cts), true);
        _cts.Dispose();
    }

    [RelayCommand]
    private async Task EncryptAllAsync()
    {
        _cts = new CancellationTokenSource();
        GamingPlatform.UserIdOutput = UserIdOutput;
        await PerformAction(() => _core.EncryptFilesAsync(InputFolderPath, GamingPlatform, _cts), true);
        _cts.Dispose();
    }

    [RelayCommand]
    private async Task ResignAllAsync()
    {
        _cts = new CancellationTokenSource();
        GamingPlatform.UserIdInput = UserIdInput;
        GamingPlatform.UserIdOutput = UserIdOutput;
        await PerformAction(() => _core.ResignFilesAsync(InputFolderPath, GamingPlatform, _cts), true);
        _cts.Dispose();
    }

    #endregion
}