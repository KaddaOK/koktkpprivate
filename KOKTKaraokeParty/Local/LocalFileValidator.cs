using System;
using System.IO;
using System.Linq;

public interface ILocalFileValidator
{
    (bool isValid, string message) IsValid(string path);
}

public class LocalFileValidator : ILocalFileValidator
{
    #region Initialized Dependencies

    private IFileWrapper FileWrapper { get; set; }
    private IZipFileManager ZipFileManager { get; set; }

    public LocalFileValidator(IFileWrapper fileWrapper, IZipFileManager zipFileManager)
    {
        FileWrapper = fileWrapper;
        ZipFileManager = zipFileManager;
    }
    public LocalFileValidator() : this(new FileWrapper(), new ZipFileManager()) {}

    #endregion

    public (bool isValid, string message) IsValid(string path)
    {
        if (!FileWrapper.Exists(path))
        {
            return (false, $"{path} does not exist.");
        }

        switch (Path.GetExtension(path).ToLower())
        {
            case ".cdg":
            case ".mp3":
                return IsPairValid(path);
            case ".zip":
                return IsZipValid(path);
            case ".mp4":
                return (true, null);
            default:
                return (false, $"Unexpected file extension for {Path.GetFileName(path)}.");
        }
    }

    private (bool isValid, string message) IsPairValid(string filepath)
    {
        var isCdg = filepath.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase);

        var otherFile = isCdg ? Path.ChangeExtension(filepath, ".mp3") : Path.ChangeExtension(filepath, ".cdg");

        if (!FileWrapper.Exists(otherFile))
        {
            return (false, $"No matching {(isCdg ? "MP3" : "CDG")} file found beside '{Path.GetFileName(filepath)}'.");
        }

        return (true, null);
    }

    private (bool isValid, string message) IsZipValid(string filepath)
    {
        var zipFileName = Path.GetFileName(filepath);

        using (var zip = ZipFileManager.OpenRead(filepath))
        {
            var cdgEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase));
            var mp3Entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));

            if (cdgEntry == null || mp3Entry == null)
            {
                return (false, $"'{zipFileName}' does not contain a .cdg and .mp3 file.");
            }

            if (Path.GetFileNameWithoutExtension(cdgEntry.FullName) != Path.GetFileNameWithoutExtension(mp3Entry.FullName))
            {
                // this is just a warning
                return (true, $"{zipFileName} names do not match (first CDG entry was '{cdgEntry.FullName}' but first MP3 entry was '{mp3Entry.FullName}').");
            }

            return (true, null);
        }
    }
}