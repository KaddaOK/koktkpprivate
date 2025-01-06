using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

public interface ICdgRendererNode : ITextureRect
{
    event CdgRendererNode.PlaybackFinishedEventHandler PlaybackFinished;

    void Play(string filepath);
    void TogglePaused(bool isPaused);
    void Stop();
}

[Meta(typeof(IAutoNode))]
public partial class CdgRendererNode : TextureRect, ICdgRendererNode
{
    public override void _Notification(int what) => this.Notify(what);

    private CdgGraphicsFile _cdgGraphic;
    private string _nowPlaying;
    private bool _isPaused;
    private bool _isLoaded;

    #region Nodes

    [Node] private AudioStreamPlayer AudioStreamPlayer { get; set; } = default!;
    [Node] private HSlider PositionSlider { get; set; } = default!;

    #endregion

    #region Signals

    [Signal] public delegate void PlaybackFinishedEventHandler(string wasPlaying);

    #endregion

    public void OnReady()
    {
        _isLoaded = false;
        PositionSlider.ValueChanged += (value) => {
            if (_isLoaded)
            {
                AudioStreamPlayer.Seek((float)value);
            }
        };
        SetProcess(true);
    }

    public async void Play(string filepath)
    {
        if (filepath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            GD.Print($"Loading from zip: {filepath}");
            await LoadFromZip(filepath);
        }
        else if (filepath.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase))
        {
            GD.Print($"Loading from cdg: {filepath}");

            var cdgPath = filepath;
            var mp3Path = Path.ChangeExtension(filepath, ".mp3");

            if (!File.Exists(mp3Path))
            {
                GD.PrintErr("No matching .mp3 file found.");
                return;
            }

            await LoadFromPath(cdgPath, mp3Path);
        }
        else if (filepath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            GD.Print($"Loading from mp3: {filepath}");

            var mp3Path = filepath;
            var cdgPath = Path.ChangeExtension(filepath, ".cdg");

            if (!File.Exists(cdgPath))
            {
                GD.PrintErr("No matching .cdg file found.");
                return;
            }

            await LoadFromPath(cdgPath, mp3Path);
        }
        else
        {
            GD.PrintErr("File must be a .cdg, .mp3, or .zip.");
            return;
        }
        _nowPlaying = filepath;
        _isLoaded = true;
        _isPaused = false;
        AudioStreamPlayer.StreamPaused = false;
        PositionSlider.MaxValue = AudioStreamPlayer.Stream.GetLength();
        AudioStreamPlayer.Play();
        AudioStreamPlayer.Finished += () => {
            Stop();
            EmitSignal(nameof(PlaybackFinished), _nowPlaying);
        };
    }

    public async Task LoadFromZip(string filepath)
    {
        using (var zip = ZipFile.OpenRead(filepath))
        {
            var cdgEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase));
            var mp3Entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));

            if (cdgEntry == null || mp3Entry == null)
            {
                GD.PrintErr("Zip file does not contain matching .cdg and .mp3 files.");
                return;
            }

            using (var cdgZipStream = cdgEntry.Open())
            using (var mp3ZipStream = mp3Entry.Open())
            {
                var cdgMemoryStream = new MemoryStream();
                GD.Print($"Copying CDG {cdgEntry.FullName} to memory stream...");
                cdgZipStream.CopyTo(cdgMemoryStream);
                GD.Print($"{cdgMemoryStream.Length} bytes copied.");

                var mp3MemoryStream = new MemoryStream();
                GD.Print($"Copying MP3 {mp3Entry.FullName} to memory stream.");
                mp3ZipStream.CopyTo(mp3MemoryStream);
                GD.Print($"{mp3MemoryStream.Length} bytes copied.");

                cdgMemoryStream.Position = 0;
                mp3MemoryStream.Position = 0;

                GD.Print("Loading CDG graphics file...");
                _cdgGraphic = await CdgGraphicsFile.LoadAsync(cdgMemoryStream);
                GD.Print("Loading MP3 audio stream...");
                var audioStream = new AudioStreamMP3();
                audioStream.Data = mp3MemoryStream.ToArray();
                AudioStreamPlayer.Stream = audioStream;
                GD.Print($"audio stream length is {audioStream.GetLength()}");
                GD.Print("LoadFromZip complete.");
            }
        }
    }

    public async Task LoadFromPath(string cdgPath, string mp3Path)
    {
        if (!File.Exists(cdgPath))
        {
            GD.PrintErr("No matching .cdg file found.");
            return;
        }
        
        if (!File.Exists(mp3Path))
        {
            GD.PrintErr("No matching .mp3 file found.");
            return;
        }

        _cdgGraphic = await CdgGraphicsFile.LoadAsync(cdgPath);
        AudioStreamPlayer.Stream = LoadMP3(mp3Path);
    }

    public void TogglePaused(bool isPaused)
    {
        if (!_isLoaded)
        {
            return;
        }

        _isPaused = isPaused;
        AudioStreamPlayer.StreamPaused = isPaused;
    }

    public void Stop()
    {
        _isPaused = true;
        AudioStreamPlayer.Stop();
        _isLoaded = false;
        Texture = null;
    }

    public void OnProcess(double delta)
    {
        if (_isPaused || !_isLoaded)
        {
            return;
        }

        // Get the current playback position in milliseconds
        var currentTime = (long)(AudioStreamPlayer.GetPlaybackPosition() * 1000);

        // Get the corresponding texture for the current time
        var texture = _cdgGraphic.RenderAtTime(currentTime);
        if (texture != null)
        {
            Texture = texture;
        }

        PositionSlider.SetValueNoSignal(AudioStreamPlayer.GetPlaybackPosition());
    }

    public AudioStreamMP3 LoadMP3(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var sound = new AudioStreamMP3();
        sound.Data = file.GetBuffer((long)file.GetLength());
        return sound;
    }
}
