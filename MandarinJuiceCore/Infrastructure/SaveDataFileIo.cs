using MandarinJuiceCore.Models.DSSS.Mandarin;

namespace MandarinJuiceCore.Infrastructure;

public static class SaveDataFileIo
{
    /// <summary>
    /// Recursively gets all files with the specified save data file extension from the input directory.
    /// </summary>
    /// <param name="inputDir">The directory to search for save data files.</param>
    /// <returns>An array of file paths matching the save data file extension.</returns>
    public static string[] GetFiles(string inputDir)
        => Directory.GetFiles(inputDir, $"*{MandarinFile.FileExtension}", SearchOption.TopDirectoryOnly);
}