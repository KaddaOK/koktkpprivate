using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

public interface ILocalFileScanner
{
    bool IsScanning { get; }
    IAsyncEnumerable<string> FindAllFilesAsync(string path, CancellationToken cancellationToken = default);
    event LocalFileScanner.UpdatePathsFoundCountEventHandler UpdatePathsFoundCount;
    event LocalFileScanner.UpdateFilesFoundCountEventHandler UpdateFilesFoundCount;
    event LocalFileScanner.UpdateOrphanedCDGFilesFoundEventHandler UpdateOrphanedCDGFilesFound;
}

public partial class LocalFileScanner : ILocalFileScanner
{
    #region Initialized Dependencies

    private IDirectoryWrapper DirectoryWrapper { get; set; }

    public void SetupForTesting(IDirectoryWrapper directoryWrapper)
    {
        DirectoryWrapper = directoryWrapper;
    }

    public void Initialize()
    {
        DirectoryWrapper = new DirectoryWrapper();
    }

    #endregion

    public delegate void UpdatePathsFoundCountEventHandler(int totalPathsFound, string currentPath);
    public event UpdatePathsFoundCountEventHandler UpdatePathsFoundCount;
    public delegate void UpdateFilesFoundCountEventHandler(int totalFilesFound);
    public event UpdateFilesFoundCountEventHandler UpdateFilesFoundCount;
    public delegate void UpdateOrphanedCDGFilesFoundEventHandler(string[] orphanedCDGFiles);
    public event UpdateOrphanedCDGFilesFoundEventHandler UpdateOrphanedCDGFilesFound;

    public bool IsScanning { get; private set; }

    public async IAsyncEnumerable<string> FindAllFilesAsync(string path, [EnumeratorCancellation]CancellationToken cancellationToken = default)
    {
        int totalPathsFound = 0;
        int totalFilesFound = 0;
        List<string> orphanedCDGFiles = new List<string>();

        var directories = new Stack<string>();
        directories.Push(path);
        IsScanning = true;
        while (directories.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var currentDir = directories.Pop();
            totalPathsFound++;
            UpdatePathsFoundCount?.Invoke(totalPathsFound, currentDir);
            var files = new List<string>();
            string[] subDirs = null;

            try
            {
                var mp4Files = DirectoryWrapper.GetFiles(currentDir, "*.mp4");
                var zipFiles = DirectoryWrapper.GetFiles(currentDir, "*.zip");

                // we only include the CDG files, and only if they have a corresponding MP3 file
                var mp3Files = DirectoryWrapper.GetFiles(currentDir, "*.mp3").ToHashSet();
                var allCdgFiles = DirectoryWrapper.GetFiles(currentDir, "*.cdg");
                var matchedCdgFiles = allCdgFiles.Where(cdg => mp3Files.Contains(Path.ChangeExtension(cdg, ".mp3")));
                var unmatchedCdgFiles = allCdgFiles.Except(matchedCdgFiles);
                if (unmatchedCdgFiles.Any())
                {
                    orphanedCDGFiles.AddRange(unmatchedCdgFiles);
                    UpdateOrphanedCDGFilesFound?.Invoke(orphanedCDGFiles.ToArray());
                }

                files = mp4Files.Concat(zipFiles).Concat(matchedCdgFiles).ToList();

                subDirs = DirectoryWrapper.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch (DirectoryNotFoundException) { }

            if (files?.Any() ?? false)
            {
                totalFilesFound += files.Count;
                UpdateFilesFoundCount?.Invoke(totalFilesFound);
                foreach (var file in files)
                {
                    yield return file;
                    await Task.Yield(); // Yield control to allow asynchronous operation
                }
            }

            if (subDirs != null)
            {
                foreach (var subDir in subDirs)
                {
                    directories.Push(subDir);
                }
            }
        }
        IsScanning = false;
    }
}

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