using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using NAudio.Flac;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KOKTKaraokeParty.Services;

[Meta(typeof(IAutoNode))]
public partial class BackgroundMusicService : Node
{
    public event Action<string> NowPlayingChanged;
    public event Action<bool> PausedStateChanged;

    private AudioStreamPlayer _musicPlayer;
    private string _currentlyPlayingPath;
    
    private Settings _settings;
    private IFileWrapper _fileWrapper;
    private IDisplayScreen _displayScreen;

    public void Initialize(Settings settings, IFileWrapper fileWrapper, IDisplayScreen displayScreen)
    {
        _settings = settings;
        _fileWrapper = fileWrapper;
        _displayScreen = displayScreen;
        SetupAudioPlayer();
    }

    public void OnReady()
    {
        this.Provide();
    }

    private void SetupAudioPlayer()
    {
        _musicPlayer = new AudioStreamPlayer();
        AddChild(_musicPlayer);
        _musicPlayer.Finished += OnMusicPlayerFinished;
        UpdateVolume();
    }

    public void SetEnabled(bool enabled)
    {
        if (_settings.BgMusicEnabled != enabled)
        {
            _settings.BgMusicEnabled = enabled;
            _settings.SaveToDisk(_fileWrapper);
        }

        if (enabled && _settings.BgMusicFiles.Count > 0)
        {
            FadeIn();
        }
        else
        {
            FadeOut();
        }
    }

    public void SetVolumePercent(double volumePercent)
    {
        _settings.BgMusicVolumePercent = volumePercent;
        _settings.SaveToDisk(_fileWrapper);
        UpdateVolume();
        GD.Print($"BG Music volume set to {volumePercent}% ({_musicPlayer.VolumeDb} dB)");
    }

    public void AddMusicFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!_settings.BgMusicFiles.Contains(path))
            {
                _settings.BgMusicFiles.Add(path);
            }
        }
        _settings.SaveToDisk(_fileWrapper);
    }

    public void RemoveMusicFile(string path)
    {
        _settings.BgMusicFiles.Remove(path);
        _settings.SaveToDisk(_fileWrapper);
        
        // If the removed item was currently playing, skip to next
        if (_currentlyPlayingPath == path && _musicPlayer.Playing)
        {
            OnMusicPlayerFinished();
        }
    }

    public async void FadeIn()
    {
        if (_settings.BgMusicVolumePercent == 0)
        {
            GD.PrintErr("BG Music volume is 0, not fading in.");
            return;
        }

        if (_musicPlayer.Playing && !_musicPlayer.StreamPaused)
        {
            GD.Print("BG Music is already playing, not fading in.");
            return;
        }

        _musicPlayer.VolumeDb = PercentToDb(0);
        var finalVolumeInDb = PercentToDb(_settings.BgMusicVolumePercent);
        GD.Print($"Fading in background music to {_settings.BgMusicVolumePercent}% ({finalVolumeInDb} dB)");
        
        var tween = GetTree().CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_musicPlayer, "volume_db", finalVolumeInDb, 2).From(PercentToDb(0.01));
        
        StartOrResume();
    }

    public async void FadeOut()
    {
        var tween = GetTree().CreateTween();
        tween.TweenProperty(_musicPlayer, "volume_db", PercentToDb(0.01), 5);
        tween.Finished += Pause;
    }

    public void StartOrResume()
    {
        if (_settings.BgMusicFiles.Count == 0) return;

        Callable.From(() => _displayScreen.UpdateBgMusicPaused(false)).CallDeferred();

        if (_musicPlayer.Playing) return;

        if (_musicPlayer.Stream == null)
        {
            StartPlaying(0);
        }

        if (_musicPlayer.StreamPaused)
        {
            GD.Print("Unpausing background music.");
            _musicPlayer.StreamPaused = false;
        }
    }

    public void Pause()
    {
        if (_musicPlayer.Playing)
        {
            _musicPlayer.StreamPaused = true;
            Callable.From(() => _displayScreen.UpdateBgMusicPaused(true)).CallDeferred();
            PausedStateChanged?.Invoke(true);
        }
    }

    private void OnMusicPlayerFinished()
    {
        if (_settings.BgMusicFiles.Count > 0)
        {
            var oldIndex = _settings.BgMusicFiles.IndexOf(_currentlyPlayingPath);
            var nextIndex = (oldIndex + 1) % _settings.BgMusicFiles.Count;
            StartPlaying(nextIndex);
        }
        else
        {
            Callable.From(() => _displayScreen.UpdateBgMusicNowPlaying("None")).CallDeferred();
            NowPlayingChanged?.Invoke("None");
        }
    }

    private void StartPlaying(int index)
    {
        if (index >= _settings.BgMusicFiles.Count) return;

        Callable.From(() => {
            _currentlyPlayingPath = _settings.BgMusicFiles[index];
            _musicPlayer.Stream = LoadAudioFromPath(_currentlyPlayingPath);
            _musicPlayer.Play();
            
            var displayName = Path.GetFileNameWithoutExtension(_currentlyPlayingPath);
            _displayScreen.UpdateBgMusicNowPlaying(displayName);
            NowPlayingChanged?.Invoke(displayName);
            _displayScreen.UpdateBgMusicPaused(false);
            PausedStateChanged?.Invoke(false);
        }).CallDeferred();
    }

    private void UpdateVolume()
    {
        _musicPlayer.VolumeDb = PercentToDb(_settings.BgMusicVolumePercent);
    }

    private float PercentToDb(double percent)
    {
        return (float)Mathf.LinearToDb(percent / 100D);
    }

    public AudioStream LoadAudioFromPath(string path)
    {
        var extension = Path.GetExtension(path).ToLower();
        return extension switch
        {
            ".ogg" => AudioStreamOggVorbis.LoadFromFile(path),
            ".mp3" => LoadMP3(path),
            ".flac" => LoadFLAC(path),
            ".wav" => LoadWAV(path),
            _ => throw new Exception("Unsupported file format: " + extension)
        };
    }

    public AudioStreamMP3 LoadMP3(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        var sound = new AudioStreamMP3();
        sound.Data = file.GetBuffer((long)file.GetLength());
        return sound;
    }

    private AudioStreamWav LoadFLAC(string path)
    {
        using var flacReader = new FlacReader(path);
        using var memoryStream = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(memoryStream, flacReader);
        return new AudioStreamWav
        {
            Data = memoryStream.ToArray(),
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            Stereo = true,
            MixRate = 44100
        };
    }

    private AudioStreamWav LoadWAV(string path)
    {
        using var wavReader = new WaveFileReader(path);
        using var memoryStream = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(memoryStream, wavReader);
        return new AudioStreamWav
        {
            Data = memoryStream.ToArray(),
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            Stereo = wavReader.WaveFormat.Channels == 2,
            MixRate = wavReader.WaveFormat.SampleRate
        };
    }
}