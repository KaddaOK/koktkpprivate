using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;

public interface IDirectoryWrapper
{
    string[] GetFiles(string path, string searchPattern);
    string[] GetDirectories(string path);
}
public class DirectoryWrapper : IDirectoryWrapper
{
    public string[] GetFiles(string path, string searchPattern)
    {
        return Directory.GetFiles(path, searchPattern);
    }

    public string[] GetDirectories(string path)
    {
        return Directory.GetDirectories(path);
    }
}

public interface IFileWrapper
{
    bool Exists(string path);
    void WriteAllText(string path, string contents);
    string ReadAllText(string path);
    FileStream Create(string path);
    void AppendAllText(string path, string contents);
}
public class FileWrapper : IFileWrapper
{
    public bool Exists(string path) => File.Exists(path);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public FileStream Create(string path) => File.Create(path);
    public void AppendAllText(string path, string contents) => File.AppendAllText(path, contents);
}

public interface IZipFileManager
{
    IZipArchive OpenRead(string archiveFileName);
}
public interface IZipArchive: IDisposable
{
    ReadOnlyCollection<IZipArchiveEntry> Entries { get; }
}
public interface IZipArchiveEntry
{
    string FullName { get; }
    Stream Open();
}

public class ZipFileManager : IZipFileManager
{
    public IZipArchive OpenRead(string archiveFileName)
    {
        return new ZipArchiveWrapper(ZipFile.OpenRead(archiveFileName));
    }
}
public class ZipArchiveWrapper : IZipArchive
{
    private readonly ZipArchive _zipArchive;

    public ZipArchiveWrapper(ZipArchive zipArchive)
    {
        _zipArchive = zipArchive;
    }

    public ReadOnlyCollection<IZipArchiveEntry> Entries => _zipArchive.Entries.Select(e => (IZipArchiveEntry)new ZipArchiveEntryWrapper(e)).ToList().AsReadOnly();

    public void Dispose()
    {
        _zipArchive.Dispose();
    }
}
public class ZipArchiveEntryWrapper : IZipArchiveEntry
{
    private readonly ZipArchiveEntry _zipArchiveEntry;

    public ZipArchiveEntryWrapper(ZipArchiveEntry zipArchiveEntry)
    {
        _zipArchiveEntry = zipArchiveEntry;
    }

    public string FullName => _zipArchiveEntry.FullName;

    public Stream Open()
    {
        return _zipArchiveEntry.Open();
    }
}