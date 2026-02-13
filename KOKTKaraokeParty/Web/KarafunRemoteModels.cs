using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KOKTKaraokeParty.Web;

#region Connection Settings

public class KarafunRemoteSettings
{
    [JsonPropertyName("host")]
    public string Host { get; set; }
    
    [JsonPropertyName("channel")]
    public string Channel { get; set; }
    
    [JsonPropertyName("uri")]
    public string Uri { get; set; }
    
    [JsonPropertyName("kcs_url")]
    public string KcsUrl { get; set; }
    
    [JsonPropertyName("hash")]
    public string Hash { get; set; }
}

#endregion

#region WebSocket Message Types

public class KarafunWebSocketMessage
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("payload")]
    public object Payload { get; set; }
}

public class KarafunWebSocketMessage<T>
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("payload")]
    public T Payload { get; set; }
}

#endregion

#region Payload Models

public class EmptyPayload { }

public class ErrorPayload
{
    [JsonPropertyName("type")]
    public int Type { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class UsernamePayload
{
    [JsonPropertyName("username")]
    public string Username { get; set; }
}

public class UpdateUsernameRequestPayload
{
    [JsonPropertyName("username")]
    public string Username { get; set; }
}

public class PermissionsPayload
{
    [JsonPropertyName("permissions")]
    public KarafunPermissions Permissions { get; set; }
}

public class KarafunPermissions
{
    [JsonPropertyName("manageQueue")]
    public bool ManageQueue { get; set; }
    
    [JsonPropertyName("viewQueue")]
    public bool ViewQueue { get; set; }
    
    [JsonPropertyName("addToQueue")]
    public bool AddToQueue { get; set; }
    
    [JsonPropertyName("managePlayback")]
    public bool ManagePlayback { get; set; }
    
    [JsonPropertyName("manageVolumes")]
    public bool ManageVolumes { get; set; }
    
    [JsonPropertyName("sendPhotos")]
    public bool SendPhotos { get; set; }
}

public class PreferencesPayload
{
    [JsonPropertyName("preferences")]
    public KarafunPreferences Preferences { get; set; }
}

public class KarafunPreferences
{
    [JsonPropertyName("askOptions")]
    public bool AskOptions { get; set; }
    
    [JsonPropertyName("singerRotation")]
    public bool SingerRotation { get; set; }
}

public class ConfigurationPayload
{
    [JsonPropertyName("configuration")]
    public KarafunConfiguration Configuration { get; set; }
}

public class KarafunConfiguration
{
    [JsonPropertyName("pitchStep")]
    public int PitchStep { get; set; }
    
    [JsonPropertyName("tempoStep")]
    public int TempoStep { get; set; }
    
    [JsonPropertyName("pitchMin")]
    public int PitchMin { get; set; }
    
    [JsonPropertyName("tempoMin")]
    public int TempoMin { get; set; }
    
    [JsonPropertyName("pitchMax")]
    public int PitchMax { get; set; }
    
    [JsonPropertyName("tempoMax")]
    public int TempoMax { get; set; }
}

#endregion

#region Status Event Models

public class StatusEventPayload
{
    [JsonPropertyName("status")]
    public KarafunStatus Status { get; set; }
}

public class KarafunStatus
{
    [JsonPropertyName("current")]
    public KarafunCurrentItem Current { get; set; }
    
    [JsonPropertyName("state")]
    public KarafunPlaybackState State { get; set; }
    
    [JsonPropertyName("pitch")]
    public int Pitch { get; set; }
    
    [JsonPropertyName("tempo")]
    public int Tempo { get; set; }
    
    [JsonPropertyName("tracks")]
    public List<KarafunTrackVolume> Tracks { get; set; }
}

public enum KarafunPlaybackState
{
    /// <summary>
    /// Empty queue / Idle
    /// </summary>
    Idle = 1,
    
    /// <summary>
    /// Loading or queue not empty but not playing
    /// </summary>
    Loading = 2,
    
    /// <summary>
    /// Playing
    /// </summary>
    Playing = 4,
    
    /// <summary>
    /// Paused
    /// </summary>
    Paused = 5
}

public class KarafunCurrentItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("song")]
    public KarafunQueueSong Song { get; set; }
}

public class KarafunQueueSong
{
    [JsonPropertyName("id")]
    public KarafunSongId Id { get; set; }
    
    [JsonPropertyName("artist")]
    public string Artist { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("songTracks")]
    public List<KarafunTrackType> SongTracks { get; set; }
    
    [JsonPropertyName("options")]
    public KarafunSongOptions Options { get; set; }
}

public class KarafunSongId
{
    [JsonPropertyName("type")]
    public int Type { get; set; }
    
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class KarafunSongOptions
{
    [JsonPropertyName("singer")]
    public string Singer { get; set; }
}

public class KarafunTrackType
{
    [JsonPropertyName("type")]
    public int Type { get; set; }
}

public class KarafunTrackVolume
{
    [JsonPropertyName("volume")]
    public int Volume { get; set; }
    
    [JsonPropertyName("track")]
    public KarafunTrackInfo Track { get; set; }
}

public class KarafunTrackInfo
{
    [JsonPropertyName("caption")]
    public string Caption { get; set; }
    
    [JsonPropertyName("type")]
    public int Type { get; set; }
}

#endregion

#region Queue Event Models

public class QueueEventPayload
{
    [JsonPropertyName("queue")]
    public KarafunQueue Queue { get; set; }
}

public class KarafunQueue
{
    [JsonPropertyName("items")]
    public List<KarafunQueueItem> Items { get; set; }
}

public class KarafunQueueItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("song")]
    public KarafunQueueSong Song { get; set; }
}

#endregion

#region Request/Response Models

public class AddToQueueRequestPayload
{
    [JsonPropertyName("song")]
    public AddToQueueSong Song { get; set; }
    
    [JsonPropertyName("options")]
    public AddToQueueOptions Options { get; set; }
}

public class AddToQueueSong
{
    [JsonPropertyName("type")]
    public int Type { get; set; }
    
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class AddToQueueOptions
{
    [JsonPropertyName("singer")]
    public string Singer { get; set; }
}

#endregion

#region Message Type Constants

public static class KarafunMessageTypes
{
    // Core messages
    public const string Authenticated = "core.AuthenticatedEvent";
    public const string PingRequest = "core.PingRequest";
    public const string PingResponse = "core.PingResponse";
    
    // Remote control events
    public const string StatusEvent = "remote.StatusEvent";
    public const string QueueEvent = "remote.QueueEvent";
    public const string PermissionsUpdate = "remote.PermissionsUpdateEvent";
    public const string PreferencesUpdate = "remote.PreferencesUpdateEvent";
    public const string ConfigurationUpdate = "remote.ConfigurationUpdateEvent";
    public const string UsernameUpdate = "remote.UsernameUpdateEvent";
    
    // Requests and responses
    public const string UpdateUsernameRequest = "remote.UpdateUsernameRequest";
    public const string UpdateUsernameResponse = "remote.UpdateUsernameResponse";
    public const string AddToQueueRequest = "remote.AddToQueueRequest";
    public const string AddToQueueResponse = "remote.AddToQueueResponse";
    public const string PauseRequest = "remote.PauseRequest";
    public const string PauseResponse = "remote.PauseResponse";
    public const string ResumeRequest = "remote.ResumeRequest";
    public const string ResumeResponse = "remote.ResumeResponse";
    public const string NextRequest = "remote.NextRequest";
    public const string NextResponse = "remote.NextResponse";
    
    // Error
    public const string Error = "Error";
}

#endregion
