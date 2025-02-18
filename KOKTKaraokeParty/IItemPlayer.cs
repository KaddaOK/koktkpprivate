
using System.Threading;
using System.Threading.Tasks;

public delegate void PlaybackFinishedEventHandler(string wasPlaying);
public delegate void PlaybackProgressEventHandler(long progressMs);
public delegate void PlaybackDurationChangedEventHandler(long durationMs);

public interface IItemPlayer
{
    bool IsPlaying { get; }
    bool IsPaused { get; }
    string CurrentPath { get; }
    long? CurrentPositionMs { get; }
    long? ItemDurationMs { get; }

    Task Start(string path, CancellationToken cancellationToken);
    //Task Pause();
    //Task Resume();
    Task TogglePaused(bool isPause);
    Task Stop();
    Task Seek(long positionMs);

    event PlaybackFinishedEventHandler PlaybackFinished;
    event PlaybackProgressEventHandler PlaybackProgress;
    event PlaybackDurationChangedEventHandler PlaybackDurationChanged;
}