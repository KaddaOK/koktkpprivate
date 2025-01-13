using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

public class LocalFileValidator
{
    public (bool isValid, string message) IsValid(string path)
    {
        if (!File.Exists(path))
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

        if (!File.Exists(otherFile))
        {
            return (false, $"No matching {(isCdg ? "MP3" : "CDG")} file found beside '{Path.GetFileName(filepath)}'.");
        }

        return (true, null);
    }

    private (bool isValid, string message) IsZipValid(string filepath)
    {
        var zipFileName = Path.GetFileName(filepath);

        using (var zip = ZipFile.OpenRead(filepath))
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

    public QueueItem GetBestGuessExternalQueueItem(string externalFilePath)
    {
        var returnItem = new QueueItem 
            {
                PerformanceLink = externalFilePath,
                CreatorName = "(drag-and-drop)",
                ItemType = Path.GetExtension(externalFilePath).ToLower() switch
                {
                    ".zip" => ItemType.LocalMp3GZip,
                    ".cdg" => ItemType.LocalMp3G,
                    ".mp3" => ItemType.LocalMp3G,
                    ".mp4" => ItemType.LocalMp4,
                    _ => throw new NotImplementedException()
                }
            };

        // TODO: this is an ignorant rush job, meh
        var components = Path.GetFileNameWithoutExtension(externalFilePath).Split(" - ");
        switch (components.Length)
        {
            case 1:
                returnItem.SongName = components[0];
                break;
            case 2:
                returnItem.ArtistName = components[0];
                returnItem.SongName = components[1];
                break;
            case 3:
                returnItem.Identifier = components[0];
                returnItem.ArtistName = components[1];
                returnItem.SongName = components[2];
                break;
            case 4:
                returnItem.CreatorName = components[0];
                returnItem.Identifier = components[1];
                returnItem.ArtistName = components[2];
                returnItem.SongName = components[3];
                break;
            default:
                throw new NotImplementedException();
        }

        return returnItem;
    }
}