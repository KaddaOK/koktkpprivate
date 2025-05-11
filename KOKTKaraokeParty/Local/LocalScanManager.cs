using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class LocalScanManager
{
    private KOKTDbContext context;
    private string scanPathString;
    private LocalScanPathEntry scanPathEntry;
    public LocalScanManager(string scanpath)
    {
        context = new KOKTDbContext();
        context.Database.EnsureCreated();
        scanPathString = scanpath;
    }

    public async Task CreateOrUpdateScanPathEntry()
    {
        scanPathEntry = context.LocalScanPaths.SingleOrDefault(lsf => lsf.Path == scanPathString);
        if (scanPathEntry == null)
        {
            scanPathEntry = new LocalScanPathEntry
            {
                Path = scanPathString
            };
            await context.AddAsync(scanPathEntry);
        }
        scanPathEntry.LastFullScanStarted = DateTime.Now;
        await context.SaveChangesAsync();
    }

    public bool CreateOrUpdateSongFileEntry(string filePath, bool saveAsYouGo = false)
    {
        if (scanPathEntry == null)
        {
            throw new InvalidOperationException("Scan path entry not created or loaded.");
        }

        var entry = context.LocalSongFiles.SingleOrDefault(lsf => lsf.FullPath == filePath);
        if (entry == null)
        {
            entry = new LocalSongFileEntry
            {
                FullPath = filePath,
                FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath),
                ParentPath = scanPathEntry
            };
            context.Add(entry);
            if (saveAsYouGo)
            {
                context.SaveChanges();
            }
            return true;
        }
        else
        {
            // in the future we might update here but only if IsCustomized is false
            return false;
        }
    }

    public void FinishAndSave()
    {
        scanPathEntry.LastFullScanCompleted = DateTime.Now;
        context.SaveChanges();
    }
}