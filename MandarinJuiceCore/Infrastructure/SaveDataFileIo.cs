using MandarinJuiceCore.Models.DSSS.Mandarin;

namespace MandarinJuiceCore.Infrastructure;

public static class SaveDataFileIo
{
    /// <summary>
    /// Gets all files with the specified save data file extension from the input directory.
    /// </summary>
    /// <param name="inputDir">The path to the directory containing the files to process.</param>
    /// <returns>An array of file paths matching the save data file extension.</returns>
    /// <exception cref="FileNotFoundException">Thrown when no files are found in the specified directory.</exception>
    public static string[] GetFiles(string inputDir)
    {
        var filesToProcess = Directory.GetFiles(inputDir, $"*{MandarinFile.FileExtension}", SearchOption.TopDirectoryOnly);
        return filesToProcess.Length == 0
            ? throw new FileNotFoundException("No files found to process.")
            : filesToProcess;
    }

    /// <summary>
    /// Gets a single file path with the specified save data file extension from the input directory.
    /// </summary>
    /// <param name="inputPath">The path to the file or directory to process.</param>
    /// <returns>The path to the file found.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the input path is not a valid directory.</exception>
    /// <exception cref="FileNotFoundException">Thrown when no files are found in the specified directory.</exception>
    public static string GetFile(string inputPath)
    {
        if (File.Exists(inputPath))
            return inputPath;
        if (!Directory.Exists(inputPath))
            throw new DirectoryNotFoundException($"The provided path '{inputPath}' is not a valid file or directory.");
        var filesToProcess = Directory.GetFiles(inputPath, $"*{MandarinFile.FileExtension}", SearchOption.TopDirectoryOnly);
        if (filesToProcess.Length == 0)
            throw new FileNotFoundException($"No '{MandarinFile.FileExtension}' files found in the provided directory '{inputPath}'.");
        return filesToProcess.FirstOrDefault() ?? string.Empty;
    }
}