using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    IAsyncEnumerable<string> FindAllFilesAsync(string path, bool includeMp4, bool includeCdgPairs, bool includeZips, CancellationToken cancellationToken = default);
    Task<List<string>> FindFirstFewFilesAsync(string path, int count, bool includeMp4, bool includeCdgPairs, bool includeZips, CancellationToken cancellationToken);
    event LocalFileScanner.UpdatePathsFoundCountEventHandler UpdatePathsFoundCount;
    event LocalFileScanner.UpdateFilesFoundCountEventHandler UpdateFilesFoundCount;
    event LocalFileScanner.UpdateOrphanedCDGFilesFoundEventHandler UpdateOrphanedCDGFilesFound;
}

public partial class LocalFileScanner : ILocalFileScanner
{
    #region Initialized Dependencies

    private IDirectoryWrapper DirectoryWrapper { get; set; }

    public LocalFileScanner(IDirectoryWrapper directoryWrapper)
    {
        DirectoryWrapper = directoryWrapper;
    }

    public LocalFileScanner() : this(new DirectoryWrapper()) { }

    #endregion

    public delegate void UpdatePathsFoundCountEventHandler(int totalPathsFound, string currentPath);
    public event UpdatePathsFoundCountEventHandler UpdatePathsFoundCount;
    public delegate void UpdateFilesFoundCountEventHandler(int totalFilesFound);
    public event UpdateFilesFoundCountEventHandler UpdateFilesFoundCount;
    public delegate void UpdateOrphanedCDGFilesFoundEventHandler(string[] orphanedCDGFiles);
    public event UpdateOrphanedCDGFilesFoundEventHandler UpdateOrphanedCDGFilesFound;

    public bool IsScanning { get; private set; }

    public async Task<List<string>> FindFirstFewFilesAsync(string path, int count, bool includeMp4, bool includeCdgPairs, bool includeZips, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        GD.Print($"FindFirstFewFilesAsync(): Starting scan of {path}...");
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var directories = new Stack<string>();
        int totalPathsFound = 0;
        directories.Push(path);
        IsScanning = true;
        await Task.Run(() => {
            while (directories.Count > 0 && files.Count < count && !cancellationToken.IsCancellationRequested)
            {
                var currentDir = directories.Pop();
                totalPathsFound++;
                //GD.Print(currentDir);
                string[] subDirs = null;

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (includeMp4)
                    {
                        files.AddRange(DirectoryWrapper.GetFiles(currentDir, "*.mp4"));

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }

                    if (includeZips)
                    {
                        files.AddRange(DirectoryWrapper.GetFiles(currentDir, "*.zip"));

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }

                    if (includeCdgPairs)
                    {
                        // we only include the CDG files, and only if they have a corresponding MP3 file
                        var mp3Files = DirectoryWrapper.GetFiles(currentDir, "*.mp3").ToHashSet();

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        var allCdgFiles = DirectoryWrapper.GetFiles(currentDir, "*.cdg");
                        var matchedCdgFiles = allCdgFiles.Where(cdg => mp3Files.Contains(Path.ChangeExtension(cdg, ".mp3")));

                        files.AddRange(matchedCdgFiles);
                    }
                    
                    if (files.Count < count)
                    {
                        subDirs = DirectoryWrapper.GetDirectories(currentDir);
                        foreach (var subDir in subDirs)
                        {
                            directories.Push(subDir);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (PathTooLongException) { }
                catch (DirectoryNotFoundException) { }
            }
        });

        GD.Print($"FindFirstFewFilesAsync(): Asked for at least {count} files, found {files.Count} across {totalPathsFound} paths in {stopwatch.Elapsed}.");
        IsScanning = false;

        return files.Take(count).ToList();
    }

    public async IAsyncEnumerable<string> FindAllFilesAsync(string path, bool includeMp4, bool includeCdgPairs, bool includeZips, [EnumeratorCancellation]CancellationToken cancellationToken)
    {
        GD.Print($"FindAllFilesAsync(): Starting scan of {path}...");
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        int totalPathsFound = 0;
        int totalFilesFound = 0;
        List<string> orphanedCDGFiles = new List<string>();

        var directories = new Stack<string>();
        directories.Push(path);
        IsScanning = true;
        int batchSize = 10; // Adjust this value as needed
        int batchCount = 0;
        while (directories.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var currentDir = directories.Pop();
            totalPathsFound++;
            GD.Print(currentDir);
            UpdatePathsFoundCount?.Invoke(totalPathsFound, currentDir);
            var files = new List<string>();
            string[] subDirs = null;

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (includeMp4)
                {
                    files.AddRange(DirectoryWrapper.GetFiles(currentDir, "*.mp4"));

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                if (includeZips)
                {
                    files.AddRange(DirectoryWrapper.GetFiles(currentDir, "*.zip"));

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                if (includeCdgPairs)
                {
                    // we only include the CDG files, and only if they have a corresponding MP3 file
                    var mp3Files = DirectoryWrapper.GetFiles(currentDir, "*.mp3").ToHashSet();

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var allCdgFiles = DirectoryWrapper.GetFiles(currentDir, "*.cdg");
                    var matchedCdgFiles = allCdgFiles.Where(cdg => mp3Files.Contains(Path.ChangeExtension(cdg, ".mp3")));
                    var unmatchedCdgFiles = allCdgFiles.Except(matchedCdgFiles);
                    if (unmatchedCdgFiles.Any())
                    {
                        orphanedCDGFiles.AddRange(unmatchedCdgFiles);
                        UpdateOrphanedCDGFilesFound?.Invoke(orphanedCDGFiles.ToArray());
                    }

                    files.AddRange(matchedCdgFiles);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                subDirs = DirectoryWrapper.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch (DirectoryNotFoundException) { }

            if (files?.Any() ?? false && !cancellationToken.IsCancellationRequested)
            {
                totalFilesFound += files.Count;
                UpdateFilesFoundCount?.Invoke(totalFilesFound);
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    yield return file;
                    batchCount++;
                    if (batchCount >= batchSize)
                    {
                        batchCount = 0;
                        await Task.Delay(1); // Yield control to allow asynchronous operation
                    }
                }
            }
            await Task.Delay(1); // Yield control to allow asynchronous operation
            if (subDirs != null && !cancellationToken.IsCancellationRequested)
            {
                foreach (var subDir in subDirs)
                {
                    directories.Push(subDir);
                }
            }
        }

        GD.Print($"FindAllFilesAsync(): Scan complete. {totalFilesFound} files found in {totalPathsFound} folders in {stopwatch.Elapsed}.");
        IsScanning = false;
    }
}
