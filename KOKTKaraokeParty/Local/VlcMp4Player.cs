using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using LibVLCSharp.Shared;

public class VlcMp4Player : IItemPlayer
{
    private IntPtr windowHandle;
    private MediaPlayer vlcMediaPlayer;
    private LibVLC libVLC;

    public bool IsPlaying => vlcMediaPlayer?.IsPlaying ?? false;

    public bool IsPaused => vlcMediaPlayer?.Media?.State == VLCState.Paused;

    public string CurrentPath { get; private set; }

    public long? CurrentPositionMs => vlcMediaPlayer?.Time;

    public long? ItemDurationMs => vlcMediaPlayer?.Length;

    public event PlaybackFinishedEventHandler PlaybackFinished;
    public event PlaybackProgressEventHandler PlaybackProgress;
    public event PlaybackDurationChangedEventHandler PlaybackDurationChanged;

    public Task Pause()
    {
        if (vlcMediaPlayer?.IsPlaying ?? false)
        {
            vlcMediaPlayer.Pause();
        }
        return Task.CompletedTask;
    }

    public Task Resume()
    {
        if (vlcMediaPlayer?.Media != null 
            && vlcMediaPlayer.Media.State == VLCState.Paused)
        {
            vlcMediaPlayer.Play();
        }
        return Task.CompletedTask;
    }

    public Task TogglePaused(bool isPause)
    {
        if (isPause)
        {
            Pause();
        }
        else
        {
            Resume();
        }
        return Task.CompletedTask;
    }

    public Task Seek(long positionMs)
    {
        if (vlcMediaPlayer?.Media != null)
        {
                if (positionMs < 0)
                {
                    positionMs = 0;
                }
                if (positionMs > vlcMediaPlayer.Media.Duration)
                {
                    positionMs = vlcMediaPlayer.Media.Duration;
                }
                vlcMediaPlayer.SeekTo(TimeSpan.FromMilliseconds(positionMs));
        }
        return Task.CompletedTask;
    }

    public async Task Start(string videoPath, CancellationToken cancellationToken)
    {
        libVLC ??= new LibVLC();
        vlcMediaPlayer = new MediaPlayer(libVLC);
        // Create a new Media instance with the path to the video file
        var media = new Media(libVLC, new Uri(videoPath)); 

        // force it to get the media's metadata (including duration, which it won't calculate otherwise)
        await media.Parse();
        PlaybackDurationChanged?.Invoke(media.Duration);
        
        // Set the window handle of the video player to the Godot window handle
        // TODO: make sure the window exists first, because if it has already dismissed then we won't get a handle here!
        windowHandle = (IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, 1);
       
        // TODO: this will only work in Windows; I think we need to set .XWindow for Linux, and ihni about macOS
        vlcMediaPlayer.Hwnd = windowHandle;

        // Play the video
        CurrentPath = videoPath;
        vlcMediaPlayer.Play(media);

        // because we awaited media.Parse(), the duration should now be known. But let's subscribe just in case
        vlcMediaPlayer.Media.DurationChanged += (sender, args) => {
            if (args.Duration > 0)
            {
                GD.Print($"Duration changed: {args.Duration}");
                PlaybackDurationChanged?.Invoke(args.Duration);
            }
        };

        vlcMediaPlayer.TimeChanged += (sender, args) => {
            if (args.Time > 0)
            {
                PlaybackProgress?.Invoke(args.Time);
            }
        };

        // Subscribe to the EndReached event so we can clean up and report back when the video ends
        vlcMediaPlayer.EndReached += MediaPlayerOnEndReached;
        
        GD.Print($"vlcMediaPlayer Hwnd: {vlcMediaPlayer.Hwnd}");
        GD.Print($"Godot native handle 0: ${DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, 0)}");
        GD.Print($"Godot native handle 1: ${DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle, 1)}");

        // We can clean up the Media instance now
        media.Dispose();
    }

    private async void MediaPlayerOnEndReached(object sender, EventArgs e)
    {
        // There will be issues if you try to clean up exactly when the video ends
        // so we'll wait a tiny bit before cleaning up
        await Task.Delay(100);
        // Clean up
        vlcMediaPlayer?.Dispose();
        vlcMediaPlayer = null;

        PlaybackFinished?.Invoke(CurrentPath);

        CurrentPath = null;
    }

    public async Task Stop()
    {
        if (vlcMediaPlayer?.IsPlaying ?? false)
        {
            vlcMediaPlayer?.Stop();
        }
        await Task.Delay(100);
        vlcMediaPlayer?.Dispose();
        vlcMediaPlayer = null;
        CurrentPath = null;
    }
}