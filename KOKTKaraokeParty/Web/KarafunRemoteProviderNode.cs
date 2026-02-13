using System;
using System.Threading;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Web;

namespace KOKTKaraokeParty;

public interface IKarafunRemoteProviderNode : INode
{
    /// <summary>
    /// Initialize the remote provider
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Current connection status
    /// </summary>
    KarafunRemoteConnectionStatus ConnectionStatus { get; }
    
    /// <summary>
    /// Current playback state from Karafun
    /// </summary>
    KarafunPlaybackState? CurrentPlaybackState { get; }
    
    /// <summary>
    /// Whether the remote client is connected and ready for commands
    /// </summary>
    new bool IsConnected { get; }
    
    /// <summary>
    /// Raised when the connection status changes
    /// </summary>
    event Action<KarafunRemoteConnectionStatus> ConnectionStatusChanged;
    
    /// <summary>
    /// Raised when the playback status changes
    /// </summary>
    event Action<KOKTKaraokeParty.Web.KarafunStatus> StatusChanged;
    
    /// <summary>
    /// Raised when an error occurs
    /// </summary>
    event Action<string> ErrorOccurred;
    
    /// <summary>
    /// Connect to Karafun using a room code
    /// </summary>
    Task<bool> ConnectAsync(string roomCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from Karafun
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Queue a song by extracting the ID from a performance URL
    /// </summary>
    Task<bool> QueueSongFromUrlAsync(string performanceUrl, string singerName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Queue a song by its ID
    /// </summary>
    Task<bool> QueueSongAsync(int songId, string singerName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pause playback
    /// </summary>
    Task<bool> PauseAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resume playback
    /// </summary>
    Task<bool> ResumeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Skip to the next song
    /// </summary>
    Task<bool> SkipAsync(CancellationToken cancellationToken = default);
}

[Meta(typeof(IAutoNode))]
public partial class KarafunRemoteProviderNode : Node, IKarafunRemoteProviderNode
{
    public override void _Notification(int what) => this.Notify(what);
    
    private IKarafunRemoteClient _remoteClient;
    
    public event Action<KarafunRemoteConnectionStatus> ConnectionStatusChanged;
    public event Action<KOKTKaraokeParty.Web.KarafunStatus> StatusChanged;
    public event Action<string> ErrorOccurred;
    
    public KarafunRemoteConnectionStatus ConnectionStatus => _remoteClient?.ConnectionStatus ?? KarafunRemoteConnectionStatus.Disconnected;
    public KarafunPlaybackState? CurrentPlaybackState => _remoteClient?.CurrentPlaybackState;
    public new bool IsConnected => _remoteClient?.ConnectionStatus == KarafunRemoteConnectionStatus.Connected;
    
    public void Initialize()
    {
        _remoteClient = new KarafunRemoteClient();
        SetupEventHandlers();
    }
    
    public void SetupForTesting(IKarafunRemoteClient remoteClient)
    {
        _remoteClient = remoteClient;
        SetupEventHandlers();
    }
    
    private void SetupEventHandlers()
    {
        _remoteClient.ConnectionStatusChanged += status =>
        {
            Callable.From(() => ConnectionStatusChanged?.Invoke(status)).CallDeferred();
        };
        
        _remoteClient.StatusChanged += status =>
        {
            Callable.From(() => StatusChanged?.Invoke(status)).CallDeferred();
        };
        
        _remoteClient.ErrorOccurred += error =>
        {
            Callable.From(() => ErrorOccurred?.Invoke(error)).CallDeferred();
        };
    }
    
    public async Task<bool> ConnectAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            GD.PrintErr("Room code is required to connect to Karafun remote");
            ErrorOccurred?.Invoke("Room code is required");
            return false;
        }
        
        // Validate room code format (should be 6 digits)
        if (roomCode.Length != 6 || !int.TryParse(roomCode, out _))
        {
            GD.PrintErr("Room code must be a 6-digit number");
            ErrorOccurred?.Invoke("Room code must be a 6-digit number");
            return false;
        }
        
        GD.Print($"Connecting to Karafun room: {roomCode}");
        return await _remoteClient.ConnectAsync(roomCode, cancellationToken);
    }
    
    public async Task DisconnectAsync()
    {
        await _remoteClient.DisconnectAsync();
    }
    
    /// <summary>
    /// Extract the song ID from a Karafun performance URL and queue the song
    /// </summary>
    /// <param name="performanceUrl">URL like https://www.karafun.com/karaoke/the-platters/the-great-pretender/9851.html</param>
    /// <param name="singerName">Name of the singer</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful</returns>
    public async Task<bool> QueueSongFromUrlAsync(string performanceUrl, string singerName, CancellationToken cancellationToken = default)
    {
        var songId = ExtractSongIdFromUrl(performanceUrl);
        if (songId == null)
        {
            GD.PrintErr($"Could not extract song ID from URL: {performanceUrl}");
            ErrorOccurred?.Invoke("Could not extract song ID from URL");
            return false;
        }
        
        return await QueueSongAsync(songId.Value, singerName, cancellationToken);
    }
    
    public async Task<bool> QueueSongAsync(int songId, string singerName, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            GD.PrintErr("Cannot queue song - not connected to Karafun remote");
            return false;
        }
        
        GD.Print($"Queueing song ID {songId} for {singerName}");
        return await _remoteClient.QueueSongAsync(songId, singerName, cancellationToken);
    }
    
    public async Task<bool> PauseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            GD.PrintErr("Cannot pause - not connected to Karafun remote");
            return false;
        }
        
        return await _remoteClient.PauseAsync(cancellationToken);
    }
    
    public async Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            GD.PrintErr("Cannot resume - not connected to Karafun remote");
            return false;
        }
        
        return await _remoteClient.ResumeAsync(cancellationToken);
    }
    
    public async Task<bool> SkipAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            GD.PrintErr("Cannot skip - not connected to Karafun remote");
            return false;
        }
        
        return await _remoteClient.SkipAsync(cancellationToken);
    }
    
    /// <summary>
    /// Extract song ID from Karafun URL.
    /// Format: https://www.karafun.com/web/?song=36600
    /// </summary>
    private static int? ExtractSongIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        
        try
        {
            var uri = new Uri(url);
            
            // Check for new format: ?song=ID
            var query = uri.Query;
            if (query.Contains("song="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(query, @"[?&]song=(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int songId))
                {
                    return songId;
                }
            }
            
            GD.PrintErr($"Could not extract song ID from URL: {url}");
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error parsing Karafun URL: {ex.Message}");
            return null;
        }
    }
    
    public override void _ExitTree()
    {
        _remoteClient?.Dispose();
        base._ExitTree();
    }
}
