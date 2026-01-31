using System;
using System.Collections.Generic;
using System.Linq;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

/// <summary>
/// Represents the mode of Karafun usage
/// </summary>
public enum KarafunMode
{
    ControlledBrowser,
    InstalledApp
}

/// <summary>
/// Represents a queue item for restoration display
/// </summary>
public class SavedQueueItemInfo
{
    public string SingerName { get; set; }
    public string SongName { get; set; }
    public string ArtistName { get; set; }
    public ItemType ItemType { get; set; }
}

/// <summary>
/// Determines which services are required based on queue contents
/// </summary>
public class RequiredServicesFromQueue
{
    public bool RequiresLocalFiles { get; set; }
    public bool RequiresYouTube { get; set; }
    public bool RequiresKarafun { get; set; }
}

/// <summary>
/// Holds all state for the session preparation wizard
/// </summary>
public class WizardState
{
    // Step A: Queue restoration
    public bool HasSavedQueue { get; set; }
    public List<SavedQueueItemInfo> SavedQueueItems { get; set; } = new();
    public QueueRestoreOption QueueRestoreChoice { get; set; } = QueueRestoreOption.NotSet;
    
    // Step B: Display settings
    public int SelectedMonitor { get; set; }
    public int AvailableMonitorCount { get; set; }
    
    // Step C: Service selection
    public bool UseLocalFiles { get; set; } = true;
    public bool UseYouTube { get; set; } = true;
    public bool UseKarafun { get; set; } = true;
    public KarafunMode KarafunMode { get; set; } = KarafunMode.ControlledBrowser;
    
    // Services required by restored queue (cannot be unchecked)
    public bool LocalFilesRequiredByQueue { get; set; }
    public bool YouTubeRequiredByQueue { get; set; }
    public bool KarafunRequiredByQueue { get; set; }
    
    // Step C info display
    public int LocalSongCount { get; set; }
    public int LocalArtistCount { get; set; }
    public int LocalPathCount { get; set; }
    
    // Step D: Session preparation results
    public bool VlcReady { get; set; }
    public bool YtDlpReady { get; set; }
    public bool BrowserReady { get; set; }
    public string VlcMessage { get; set; }
    public string YtDlpMessage { get; set; }
    public string BrowserMessage { get; set; }
    
    // Step E: Karafun launch
    public bool KarafunWebPlayerLaunched { get; set; }
    
    // Step F: Remote control
    public string KarafunRoomCode { get; set; }
    public bool KarafunRemoteConnected { get; set; }
    public string KarafunRemoteMessage { get; set; }
}

public enum QueueRestoreOption
{
    NotSet,
    StartFresh,
    YesExceptFirst,
    YesAll
}

/// <summary>
/// Pure logic class for wizard decisions - no Godot dependencies for easy testing
/// </summary>
public static class WizardLogic
{
    /// <summary>
    /// Determines if Step A should be shown
    /// </summary>
    public static bool ShouldShowRestoreQueueStep(WizardState state)
    {
        return state.HasSavedQueue && state.SavedQueueItems.Count > 0;
    }
    
    /// <summary>
    /// Determines which services are required based on the items being restored
    /// </summary>
    public static RequiredServicesFromQueue GetRequiredServicesFromQueue(
        List<SavedQueueItemInfo> items, 
        QueueRestoreOption restoreOption)
    {
        var result = new RequiredServicesFromQueue();
        
        if (restoreOption == QueueRestoreOption.StartFresh || restoreOption == QueueRestoreOption.NotSet)
        {
            return result; // Nothing required
        }
        
        var itemsToCheck = restoreOption == QueueRestoreOption.YesExceptFirst && items.Count > 1
            ? items.Skip(1).ToList()
            : items;
        
        foreach (var item in itemsToCheck)
        {
            switch (item.ItemType)
            {
                case ItemType.LocalMp3G:
                case ItemType.LocalMp3GZip:
                case ItemType.LocalMp4:
                    result.RequiresLocalFiles = true;
                    break;
                case ItemType.Youtube:
                    result.RequiresYouTube = true;
                    break;
                case ItemType.KarafunWeb:
                    result.RequiresKarafun = true;
                    break;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Determines if VLC session preparation is needed
    /// </summary>
    public static bool NeedsVlcPrepare(WizardState state)
    {
        // VLC is needed for local files AND for YouTube (downloaded playback)
        return state.UseLocalFiles || state.UseYouTube;
    }
    
    /// <summary>
    /// Determines if yt-dlp session preparation is needed
    /// </summary>
    public static bool NeedsYtDlpPrepare(WizardState state)
    {
        return state.UseYouTube;
    }
    
    /// <summary>
    /// Determines if browser status check is needed
    /// </summary>
    public static bool NeedsBrowserCheck(WizardState state)
    {
        // Browser is only needed for Karafun with Controlled Browser mode
        return state.UseKarafun && state.KarafunMode == KarafunMode.ControlledBrowser;
    }
    
    /// <summary>
    /// Determines if Karafun steps (E, F, G) should be shown
    /// </summary>
    public static bool ShouldShowKarafunSteps(WizardState state)
    {
        return state.UseKarafun;
    }
    
    /// <summary>
    /// Determines if any preparation is needed (Step D should be shown)
    /// </summary>
    public static bool NeedsAnyPreparation(WizardState state)
    {
        return NeedsVlcPrepare(state) || NeedsYtDlpPrepare(state) || NeedsBrowserCheck(state);
    }
    
    /// <summary>
    /// Determines if the Next button should be enabled on Step C
    /// </summary>
    public static bool IsStepCNextEnabled(WizardState state)
    {
        // At least one service must be selected
        return state.UseLocalFiles || state.UseYouTube || state.UseKarafun;
    }
    
    /// <summary>
    /// Determines if Step D can auto-continue (all checks passed)
    /// </summary>
    public static bool CanAutoAdvanceFromStepD(WizardState state)
    {
        // Check only the services that were required
        if (NeedsVlcPrepare(state) && !state.VlcReady)
            return false;
        if (NeedsYtDlpPrepare(state) && !state.YtDlpReady)
            return false;
        if (NeedsBrowserCheck(state) && !state.BrowserReady)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Determines if Step E Next button should be enabled
    /// </summary>
    public static bool IsStepENextEnabled(WizardState state)
    {
        // If using controlled browser, must have launched
        // If using installed app, always enabled (user manages it themselves)
        if (state.KarafunMode == KarafunMode.ControlledBrowser)
        {
            return state.KarafunWebPlayerLaunched;
        }
        return true;
    }
    
    /// <summary>
    /// Determines if Step F Next button should be enabled
    /// </summary>
    public static bool IsStepFNextEnabled(WizardState state)
    {
        return state.KarafunRemoteConnected;
    }
    
    /// <summary>
    /// Gets the initial tab to navigate to after wizard completion
    /// </summary>
    public static WizardDestination GetWizardDestination(WizardState state)
    {
        // If local files is selected but no songs are scanned, go to Local Files tab
        if (state.UseLocalFiles && state.LocalSongCount == 0)
        {
            return WizardDestination.LocalFilesTab;
        }
        
        return WizardDestination.SearchTab;
    }
    
    /// <summary>
    /// Validates that a room code appears valid (6 digits)
    /// </summary>
    public static bool IsValidRoomCodeFormat(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            return false;
        
        return roomCode.Length == 6 && roomCode.All(char.IsDigit);
    }
}

public enum WizardDestination
{
    SearchTab,
    LocalFilesTab
}
