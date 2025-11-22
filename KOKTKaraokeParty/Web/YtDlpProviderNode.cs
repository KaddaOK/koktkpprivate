using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

namespace KOKTKaraokeParty;

public interface IYtDlpProviderNode : INode
{
	Task CheckStatus();
	event YtDlpStatusEventHandler YtDlpStatusChecked;
	
	Task<string> DownloadFromUrl(string url, string outputPath);
	string GetYtDlpExecutablePath();
	string GetDenoExecutablePath();
}

public enum YtDlpStatus
{
	NotStarted,
	Checking,
	Downloading,
	Ready,
	FatalError
}

public delegate void YtDlpStatusEventHandler(StatusCheckResult<YtDlpStatus> status);

[Meta(typeof(IAutoNode))]
public partial class YtDlpProviderNode : Node, IYtDlpProviderNode
{
	public override void _Notification(int what) => this.Notify(what);

	public event YtDlpStatusEventHandler YtDlpStatusChecked;

	private static readonly System.Net.Http.HttpClient httpClient = new();

	#region Initialized Dependencies

	private IFileWrapper FileWrapper { get; set; }

	public void SetupForTesting(IFileWrapper fileWrapper)
	{
		FileWrapper = fileWrapper;
	}

	public void Initialize()
	{
		FileWrapper = new FileWrapper();
	}

	#endregion

	public async Task CheckStatus()
	{
		try
		{
			GD.Print("Checking yt-dlp and deno status...");
			YtDlpStatusChecked?.Invoke(
				new StatusCheckResult<YtDlpStatus>(
					YtDlpStatus.Checking,
					null,
					null));

			var ytDlpPath = await EnsureYtDlp();
			var denoPath = await EnsureDeno();

			GD.Print($"yt-dlp ready at {ytDlpPath}, deno ready at {denoPath}");
			YtDlpStatusChecked?.Invoke(
				new StatusCheckResult<YtDlpStatus>(
					YtDlpStatus.Ready,
					$"yt-dlp and deno ready",
					$"{ytDlpPath}"));
		}
		catch (Exception ex)
		{
			GD.PrintErr($"yt-dlp/deno status check failed: {ex.Message}");
			YtDlpStatusChecked?.Invoke(
				new StatusCheckResult<YtDlpStatus>(YtDlpStatus.FatalError, null, ex.Message));
		}
	}

	private async Task<string> EnsureYtDlp()
	{
		var ytDlpDir = GetYtDlpDirectory();
		Directory.CreateDirectory(ytDlpDir);

		var ytDlpExecutablePath = GetYtDlpExecutablePath();

		if (!FileWrapper.Exists(ytDlpExecutablePath))
		{
			GD.Print("Downloading yt-dlp...");
			YtDlpStatusChecked?.Invoke(
				new StatusCheckResult<YtDlpStatus>(
					YtDlpStatus.Downloading,
					null,
					"Downloading yt-dlp..."));

			await DownloadYtDlp(ytDlpExecutablePath);

			// Make executable on Unix systems
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				MakeFileExecutable(ytDlpExecutablePath);
			}
		}

		return ytDlpExecutablePath;
	}

	private async Task<string> EnsureDeno()
	{
		var denoDir = GetYtDlpDirectory(); // Use same directory as yt-dlp
		Directory.CreateDirectory(denoDir);

		var denoExecutablePath = GetDenoExecutablePath();

		if (!FileWrapper.Exists(denoExecutablePath))
		{
			GD.Print("Downloading deno...");
			YtDlpStatusChecked?.Invoke(
				new StatusCheckResult<YtDlpStatus>(
					YtDlpStatus.Downloading,
					null,
					"Downloading deno..."));

			await DownloadDeno(denoExecutablePath);

			// Make executable on Unix systems
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				MakeFileExecutable(denoExecutablePath);
			}
		}

		return denoExecutablePath;
	}

	private async Task DownloadYtDlp(string destinationPath)
	{
		string downloadUrl;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			// For macOS, we'll use the universal binary
			downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
		}
		else
		{
			throw new PlatformNotSupportedException("Unsupported operating system for yt-dlp download");
		}

		await DownloadFile(downloadUrl, destinationPath);
	}

	private async Task DownloadDeno(string destinationPath)
	{
		string downloadUrl;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			// For Windows, we need the zip file and extract it
			var zipPath = destinationPath + ".zip";
			downloadUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
			await DownloadFile(downloadUrl, zipPath);
			ExtractDenoWindows(zipPath, destinationPath);
			File.Delete(zipPath);
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			var zipPath = destinationPath + ".zip";
			downloadUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-unknown-linux-gnu.zip";
			await DownloadFile(downloadUrl, zipPath);
			ExtractDenoUnix(zipPath, destinationPath);
			File.Delete(zipPath);
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			var zipPath = destinationPath + ".zip";
			downloadUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-apple-darwin.zip";
			await DownloadFile(downloadUrl, zipPath);
			ExtractDenoUnix(zipPath, destinationPath);
			File.Delete(zipPath);
		}
		else
		{
			throw new PlatformNotSupportedException("Unsupported operating system for deno download");
		}
	}

	private async Task DownloadFile(string url, string destinationPath)
	{
		using var response = await httpClient.GetAsync(url);
		response.EnsureSuccessStatusCode();
		
		using var fileStream = FileWrapper.Create(destinationPath);
		await response.Content.CopyToAsync(fileStream);
	}

	private void ExtractDenoWindows(string zipPath, string destinationPath)
	{
		// Extract deno.exe from the zip file on Windows
		using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
		var denoEntry = archive.GetEntry("deno.exe");
		if (denoEntry != null)
		{
			using var entryStream = denoEntry.Open();
			using var fileStream = FileWrapper.Create(destinationPath);
			entryStream.CopyTo(fileStream);
		}
		else
		{
			throw new InvalidOperationException("deno.exe not found in the downloaded archive");
		}
	}

	private void ExtractDenoUnix(string zipPath, string destinationPath)
	{
		// Extract deno binary from the zip file on Unix systems
		using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
		var denoEntry = archive.GetEntry("deno");
		if (denoEntry != null)
		{
			using var entryStream = denoEntry.Open();
			using var fileStream = FileWrapper.Create(destinationPath);
			entryStream.CopyTo(fileStream);
		}
		else
		{
			throw new InvalidOperationException("deno binary not found in the downloaded archive");
		}
	}

	private void MakeFileExecutable(string filePath)
	{
		// Make the file executable on Unix systems
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "chmod",
					Arguments = $"+x \"{filePath}\"",
					UseShellExecute = false,
					CreateNoWindow = true
				}
			};
			process.Start();
			process.WaitForExit();
		}
	}

	public string GetYtDlpExecutablePath()
	{
		var ytDlpDir = GetYtDlpDirectory();
		var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "yt-dlp.exe" : 
							RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "yt-dlp_macos" : "yt-dlp";
		return Path.Combine(ytDlpDir, executableName);
	}

	public string GetDenoExecutablePath()
	{
		var ytDlpDir = GetYtDlpDirectory(); // Use same directory as yt-dlp
		var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "deno.exe" : "deno";
		return Path.Combine(ytDlpDir, executableName);
	}

	private string GetYtDlpDirectory()
	{
		return ProjectSettings.GlobalizePath("user://yt-dlp");
	}

	public async Task<string> DownloadFromUrl(string url, string outputPath)
	{
		var ytDlpPath = GetYtDlpExecutablePath();

		var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = ytDlpPath,
				Arguments = $"\"{url}\" -o \"{outputPath}\" --force-overwrites",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			}
		};

		// Forward stdout to GD.Print in real-time
		process.OutputDataReceived += (sender, e) =>
		{
			if (!string.IsNullOrEmpty(e.Data))
			{
				GD.Print($"yt-dlp: {e.Data}");
			}
		};

		process.ErrorDataReceived += (sender, e) =>
		{
			if (!string.IsNullOrEmpty(e.Data))
			{
				GD.PrintErr($"yt-dlp: {e.Data}");
			}
		};

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		
		await process.WaitForExitAsync();

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"yt-dlp failed with exit code {process.ExitCode}");
		}

		return "Download completed";
	}
}
