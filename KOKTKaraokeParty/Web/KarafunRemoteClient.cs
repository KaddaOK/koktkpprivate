using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using KOKTKaraokeParty.Web;

public interface IKarafunRemoteClient : IDisposable
{
    /// <summary>
    /// Raised when the connection status changes
    /// </summary>
    event Action<KarafunRemoteConnectionStatus> ConnectionStatusChanged;
    
    /// <summary>
    /// Raised when the playback status changes
    /// </summary>
    event Action<KOKTKaraokeParty.Web.KarafunStatus> StatusChanged;
    
    /// <summary>
    /// Raised when the queue changes
    /// </summary>
    event Action<KarafunQueue> QueueChanged;
    
    /// <summary>
    /// Raised when an error occurs
    /// </summary>
    event Action<string> ErrorOccurred;
    
    /// <summary>
    /// Current connection status
    /// </summary>
    KarafunRemoteConnectionStatus ConnectionStatus { get; }
    
    /// <summary>
    /// Current playback state
    /// </summary>
    KarafunPlaybackState? CurrentPlaybackState { get; }
    
    /// <summary>
    /// Current playing item
    /// </summary>
    KarafunCurrentItem CurrentItem { get; }
    
    /// <summary>
    /// Current queue
    /// </summary>
    KarafunQueue CurrentQueue { get; }
    
    /// <summary>
    /// Connect to a Karafun remote control session using a room code
    /// </summary>
    Task<bool> ConnectAsync(string roomCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from the current session
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Queue a song by its ID
    /// </summary>
    Task<bool> QueueSongAsync(int songId, string singerName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Pause the current playback
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
    
    /// <summary>
    /// Set the username for this remote session
    /// </summary>
    Task<bool> SetUsernameAsync(string username, CancellationToken cancellationToken = default);
}

public enum KarafunRemoteConnectionStatus
{
    Disconnected,
    FetchingSettings,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

public class KarafunRemoteClient : IKarafunRemoteClient
{
    private const string KarafunBaseUrl = "https://www.karafun.com";
    private const string WebSocketSubProtocol = "kcpj~v2+emuping";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    
    private ClientWebSocket _webSocket;
    private CookieContainer _cookies;
    private KarafunRemoteSettings _settings;
    private CancellationTokenSource _receiveCts;
    private Task _receiveTask;
    private int _nextMessageId = 1;
    private readonly object _messageLock = new object();
    
    public event Action<KarafunRemoteConnectionStatus> ConnectionStatusChanged;
    public event Action<KOKTKaraokeParty.Web.KarafunStatus> StatusChanged;
    public event Action<KarafunQueue> QueueChanged;
    public event Action<string> ErrorOccurred;
    
    public KarafunRemoteConnectionStatus ConnectionStatus { get; private set; } = KarafunRemoteConnectionStatus.Disconnected;
    public KarafunPlaybackState? CurrentPlaybackState { get; private set; }
    public KarafunCurrentItem CurrentItem { get; private set; }
    public KarafunQueue CurrentQueue { get; private set; }
    
    private void SetConnectionStatus(KarafunRemoteConnectionStatus status)
    {
        if (ConnectionStatus != status)
        {
            ConnectionStatus = status;
            ConnectionStatusChanged?.Invoke(status);
        }
    }
    
    public async Task<bool> ConnectAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Clean up any existing connection
            await DisconnectAsync();
            
            _cookies = new CookieContainer();
            
            // Step 1: Fetch settings from the room page
            SetConnectionStatus(KarafunRemoteConnectionStatus.FetchingSettings);
            _settings = await FetchSettingsAsync(roomCode, cancellationToken);
            
            if (_settings?.KcsUrl == null || _settings?.Hash == null)
            {
                GD.PrintErr("Failed to fetch Karafun settings - missing kcs_url or hash");
                SetConnectionStatus(KarafunRemoteConnectionStatus.Error);
                ErrorOccurred?.Invoke("Failed to fetch remote control settings from Karafun");
                return false;
            }
            
            GD.Print($"Karafun settings fetched - kcs_url: {_settings.KcsUrl}");
            
            // Step 2: Connect WebSocket
            SetConnectionStatus(KarafunRemoteConnectionStatus.Connecting);
            var connected = await OpenWebSocketAsync(cancellationToken);
            
            if (!connected)
            {
                SetConnectionStatus(KarafunRemoteConnectionStatus.Error);
                ErrorOccurred?.Invoke("Failed to connect to Karafun WebSocket");
                return false;
            }
            
            // Step 3: Start receive loop
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
            
            // Step 4: Set username (required before queueing songs)
            await UpdateUsernameAsync("koktkp", cancellationToken);
            
            SetConnectionStatus(KarafunRemoteConnectionStatus.Connected);
            GD.Print("Connected to Karafun remote control");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to connect to Karafun: {ex.Message}");
            SetConnectionStatus(KarafunRemoteConnectionStatus.Error);
            ErrorOccurred?.Invoke($"Connection failed: {ex.Message}");
            return false;
        }
    }
    
    private async Task<KarafunRemoteSettings> FetchSettingsAsync(string roomCode, CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler { CookieContainer = _cookies };
        using var http = new System.Net.Http.HttpClient(handler);
        
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        
        var url = $"{KarafunBaseUrl}/{roomCode}/";
        GD.Print($"Fetching settings from {url}");
        
        var html = await http.GetStringAsync(url, cancellationToken);
        
        // Extract: const Settings = { ... };
        var match = Regex.Match(
            html,
            @"const\s+Settings\s*=\s*(\{.*?\});",
            RegexOptions.Singleline);
        
        if (!match.Success)
        {
            GD.PrintErr("Settings block not found in Karafun page");
            return null;
        }
        
        var settingsJsObject = match.Groups[1].Value;
        GD.Print($"Found Settings block: {settingsJsObject.Substring(0, Math.Min(200, settingsJsObject.Length))}...");
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        var settings = JsonSerializer.Deserialize<KarafunRemoteSettings>(settingsJsObject, options);
        
        return settings;
    }
    
    private async Task<bool> OpenWebSocketAsync(CancellationToken cancellationToken)
    {
        try
        {
            var uri = new Uri(_settings.KcsUrl);
            
            _webSocket = new ClientWebSocket();
            _webSocket.Options.Cookies = _cookies;
            _webSocket.Options.SetRequestHeader("Origin", KarafunBaseUrl);
            _webSocket.Options.SetRequestHeader("Host", uri.Host);
            _webSocket.Options.SetRequestHeader("User-Agent", UserAgent);
            _webSocket.Options.AddSubProtocol(WebSocketSubProtocol);
            
            await _webSocket.ConnectAsync(uri, cancellationToken);
            
            return _webSocket.State == WebSocketState.Open;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"WebSocket connection failed: {ex.Message}");
            return false;
        }
    }
    
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && 
                   _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    GD.Print("WebSocket closed by server");
                    SetConnectionStatus(KarafunRemoteConnectionStatus.Disconnected);
                    break;
                }
                
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();
                    
                    await HandleMessageAsync(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            GD.Print("Receive loop cancelled");
        }
        catch (WebSocketException ex)
        {
            GD.PrintErr($"WebSocket error: {ex.Message}");
            SetConnectionStatus(KarafunRemoteConnectionStatus.Error);
            ErrorOccurred?.Invoke($"WebSocket error: {ex.Message}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Receive loop error: {ex.Message}");
        }
    }
    
    private async Task HandleMessageAsync(string messageJson)
    {
        try
        {
            // Log incoming message for debugging (truncate if too long)
            /*
            var logMessage = messageJson.Length > 500 
                ? messageJson.Substring(0, 500) + "..." 
                : messageJson;
            GD.Print($"KarafunRemote received: {logMessage}");
            */
            
            var message = JsonSerializer.Deserialize<KarafunWebSocketMessage>(
                messageJson, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (message == null) return;
            
            switch (message.Type)
            {
                case KarafunMessageTypes.PingRequest:
                    await HandlePingAsync(message.Id ?? 0);
                    break;
                    
                case KarafunMessageTypes.StatusEvent:
                    HandleStatusEvent(messageJson);
                    break;
                    
                case KarafunMessageTypes.QueueEvent:
                    HandleQueueEvent(messageJson);
                    break;
                    
                case KarafunMessageTypes.Authenticated:
                    GD.Print("Karafun remote authenticated");
                    break;
                    
                case KarafunMessageTypes.PermissionsUpdate:
                case KarafunMessageTypes.PreferencesUpdate:
                case KarafunMessageTypes.ConfigurationUpdate:
                case KarafunMessageTypes.UsernameUpdate:
                    // These events are informational, we don't need to act on them
                    GD.Print($"Received {message.Type}");
                    break;
                    
                case KarafunMessageTypes.Error:
                    HandleError(messageJson);
                    break;
                    
                default:
                    if (message.Type.EndsWith("Response"))
                    {
                        // Response messages are handled by the request waiting for them
                        GD.Print($"Received response: {message.Type}");
                    }
                    else
                    {
                        GD.Print($"Unknown message type: {message.Type}");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error handling message: {ex.Message}");
        }
    }
    
    private async Task HandlePingAsync(int pingId)
    {
        var response = new KarafunWebSocketMessage
        {
            Id = pingId,
            Type = KarafunMessageTypes.PingResponse,
            Payload = new { }
        };
        
        await SendMessageAsync(response);
    }
    
    private void HandleStatusEvent(string messageJson)
    {
        try
        {
            var statusMessage = JsonSerializer.Deserialize<KarafunWebSocketMessage<StatusEventPayload>>(
                messageJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (statusMessage?.Payload?.Status != null)
            {
                CurrentPlaybackState = statusMessage.Payload.Status.State;
                CurrentItem = statusMessage.Payload.Status.Current;
                StatusChanged?.Invoke(statusMessage.Payload.Status);
                
                GD.Print($"Karafun status: State={CurrentPlaybackState}, Current={CurrentItem?.Song?.Title ?? "none"}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error parsing status event: {ex.Message}");
        }
    }
    
    private void HandleQueueEvent(string messageJson)
    {
        try
        {
            var queueMessage = JsonSerializer.Deserialize<KarafunWebSocketMessage<QueueEventPayload>>(
                messageJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (queueMessage?.Payload?.Queue != null)
            {
                CurrentQueue = queueMessage.Payload.Queue;
                QueueChanged?.Invoke(queueMessage.Payload.Queue);
                
                GD.Print($"Karafun queue updated: {CurrentQueue.Items?.Count ?? 0} items");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error parsing queue event: {ex.Message}");
        }
    }
    
    private void HandleError(string messageJson)
    {
        try
        {
            var errorMessage = JsonSerializer.Deserialize<KarafunWebSocketMessage<ErrorPayload>>(
                messageJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (errorMessage?.Payload?.Message != null)
            {
                GD.PrintErr($"Karafun error: {errorMessage.Payload.Message}");
                ErrorOccurred?.Invoke(errorMessage.Payload.Message);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error parsing error message: {ex.Message}");
        }
    }
    
    private async Task SendMessageAsync(object message)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            GD.PrintErr("Cannot send message - WebSocket not connected");
            return;
        }
        
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        //GD.Print($"KarafunRemote sending: {json}");
        
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }
    
    private int GetNextMessageId()
    {
        lock (_messageLock)
        {
            return _nextMessageId++;
        }
    }
    
    private async Task UpdateUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var request = new KarafunWebSocketMessage<UpdateUsernameRequestPayload>
        {
            Id = GetNextMessageId(),
            Type = KarafunMessageTypes.UpdateUsernameRequest,
            Payload = new UpdateUsernameRequestPayload { Username = username }
        };
        
        GD.Print($"Sending username update: {username}");
        await SendMessageAsync(request);
    }
    
    public async Task<bool> QueueSongAsync(int songId, string singerName, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            GD.PrintErr("Cannot queue song - not connected");
            return false;
        }
        
        var request = new
        {
            id = GetNextMessageId(),
            type = KarafunMessageTypes.AddToQueueRequest,
            payload = new
            {
                song = new { type = 1, id = songId },
                options = new { singer = singerName }
            }
        };
        
        await SendMessageAsync(request);
        return true;
    }
    
    public async Task<bool> PauseAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            GD.PrintErr("Cannot pause - not connected");
            return false;
        }
        
        var request = new
        {
            id = GetNextMessageId(),
            type = KarafunMessageTypes.PauseRequest,
            payload = new { }
        };
        
        await SendMessageAsync(request);
        return true;
    }
    
    public async Task<bool> ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            GD.PrintErr("Cannot resume - not connected");
            return false;
        }
        
        var request = new
        {
            id = GetNextMessageId(),
            type = KarafunMessageTypes.ResumeRequest,
            payload = new { }
        };
        
        await SendMessageAsync(request);
        return true;
    }
    
    public async Task<bool> SkipAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            GD.PrintErr("Cannot skip - not connected");
            return false;
        }
        
        var request = new
        {
            id = GetNextMessageId(),
            type = KarafunMessageTypes.NextRequest,
            payload = new { }
        };
        
        await SendMessageAsync(request);
        return true;
    }
    
    public async Task<bool> SetUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
        {
            GD.PrintErr("Cannot set username - not connected");
            return false;
        }
        
        var request = new
        {
            id = GetNextMessageId(),
            type = KarafunMessageTypes.UpdateUsernameRequest,
            payload = new { username = username }
        };
        
        await SendMessageAsync(request);
        return true;
    }
    
    public async Task DisconnectAsync()
    {
        try
        {
            _receiveCts?.Cancel();
            
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Client disconnecting", 
                    CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error during disconnect: {ex.Message}");
        }
        finally
        {
            _webSocket?.Dispose();
            _webSocket = null;
            _receiveCts?.Dispose();
            _receiveCts = null;
            _settings = null;
            _cookies = null;
            _nextMessageId = 1;
            CurrentPlaybackState = null;
            CurrentItem = null;
            CurrentQueue = null;
            SetConnectionStatus(KarafunRemoteConnectionStatus.Disconnected);
        }
    }
    
    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}
