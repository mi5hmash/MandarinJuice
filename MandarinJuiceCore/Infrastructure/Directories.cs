using System.Diagnostics;

namespace MandarinJuiceCore.Infrastructure;

/// <summary>
/// Provides utility methods and properties for managing application directory paths, including the root and output directories.
/// </summary>
public static class Directories
{
    public static readonly string RootPath = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Output = Path.Combine(RootPath, "_OUTPUT");
    public static readonly string Profiles = Path.Combine(RootPath, "_profiles");

    /// <summary>
    /// Generates a new output directory path using the current date and time, combined with the specified action name.
    /// </summary>
    /// <param name="action">The name of the action to include in the output directory path.</param>
    /// <returns>A string representing the full path of the new output directory, formatted with the current date, time, and the specified action.</returns>
    public static string GetNewOutputDirectory(string action)
        => Path.Combine(Output, $"{DateTime.Now:yyyy-MM-dd_HHmmssfff}_{action}");

    /// <param name="path">A path to process.</param>
    extension(string path)
    {
        /// <summary>
        /// Combines the specified output directory path with a user identifier to create a user-specific subdirectory path.
        /// </summary>
        /// <param name="userId">The user identifier to append to the output directory path.</param>
        /// <returns>A string representing the combined path of the output directory and user identifier.</returns>
        public string AddUserId(string userId)
            => Path.Combine(path, userId);

        /// <summary>
        /// Removes trailing directory separator characters from a path.
        /// </summary>
        /// <returns>The path without trailing directory separators.</returns>
        public string TrimDirectorySeparator()
            => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Opens the specified directory in the system's default file explorer, if the directory exists.
    /// </summary>
    /// <param name="path">The full path of the directory to open.</param>
    public static void OpenDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string? openCmd = null;
        string? args = null;

        Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows())
        {
            openCmd = "explorer.exe";
            args = $"\"{path}\"";
        }
        else if (OperatingSystem.IsMacOS())
        {
            openCmd = "open";
            args = $"\"{path}\"";
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
        {
            openCmd = "xdg-open";
            args = $"\"{path}\"";
        }

        if (openCmd != null && args != null)
            Process.Start(new ProcessStartInfo
            {
                FileName = openCmd,
                Arguments = args,
                UseShellExecute = false
            });
    }
}