using MandarinJuiceCore.Models.DSSS.Mandarin;

namespace MandarinJuiceCore.Infrastructure;

public static class SaveDataFileIo
{
    /// <summary>
    /// Recursively gets all files with the specified save data file extension from the input directory and its subdirectories.
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
}