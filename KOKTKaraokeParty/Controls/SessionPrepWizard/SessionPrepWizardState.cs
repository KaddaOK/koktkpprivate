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
public class SessionPrepWizardState
{
    // Step 1: Queue restoration
    public bool HasSavedQueue { get; set; }
    public List<SavedQueueItemInfo> SavedQueueItems { get; set; } = new();
    public QueueRestoreOption QueueRestoreChoice { get; set; } = QueueRestoreOption.NotSet;
    
    // Step 2: Display settings
    public int SelectedMonitor { get; set; }
    public int AvailableMonitorCount { get; set; }
    
    // Step 3: Service selection
    public bool UseLocalFiles { get; set; } = true;
    public bool UseYouTube { get; set; } = true;
    public bool UseKarafun { get; set; } = true;
    public KarafunMode KarafunMode { get; set; } = KarafunMode.ControlledBrowser;
    
    // Services required by restored queue (cannot be unchecked)
    public bool LocalFilesRequiredByQueue { get; set; }
    public bool YouTubeRequiredByQueue { get; set; }
    public bool KarafunRequiredByQueue { get; set; }
    
    // Step 3 info display
    public int LocalSongCount { get; set; }
    public int LocalArtistCount { get; set; }
    public int LocalPathCount { get; set; }
    
    // Step 4: Session preparation results
    public bool VlcReady { get; set; }
    public bool YtDlpReady { get; set; }
    public bool BrowserReady { get; set; }
    public string VlcMessage { get; set; }
    public string YtDlpMessage { get; set; }
    public string BrowserMessage { get; set; }
    
    // Step 5: Karafun launch
    public bool KarafunWebPlayerLaunched { get; set; }
    
    // Step 6: Remote control
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
public static class SessionPrepWizardLogic
{
    /// <summary>
    /// Determines if Step 1 should be shown
    /// </summary>
    public static bool ShouldShowRestoreQueueStep(SessionPrepWizardState state)
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
                case ItemType.KarafunRemote:
                    result.RequiresKarafun = true;
                    break;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Determines if VLC session preparation is needed
    /// </summary>
    public static bool NeedsVlcPrepare(SessionPrepWizardState state)
    {
        // VLC is needed for local files AND for YouTube (downloaded playback)
        return state.UseLocalFiles || state.UseYouTube;
    }
    
    /// <summary>
    /// Determines if yt-dlp session preparation is needed
    /// </summary>
    public static bool NeedsYtDlpPrepare(SessionPrepWizardState state)
    {
        return state.UseYouTube;
    }
    
    /// <summary>
    /// Determines if browser status check is needed
    /// </summary>
    public static bool NeedsBrowserCheck(SessionPrepWizardState state)
    {
        // Browser is only needed for Karafun with Controlled Browser mode
        return state.UseKarafun && state.KarafunMode == KarafunMode.ControlledBrowser;
    }
    
    /// <summary>
    /// Determines if Karafun steps (5, 6, 7) should be shown
    /// </summary>
    public static bool ShouldShowKarafunSteps(SessionPrepWizardState state)
    {
        return state.UseKarafun;
    }
    
    /// <summary>
    /// Determines if any preparation is needed (Step 4 should be shown)
    /// </summary>
    public static bool NeedsAnyPreparation(SessionPrepWizardState state)
    {
        return NeedsVlcPrepare(state) || NeedsYtDlpPrepare(state) || NeedsBrowserCheck(state);
    }
    
    /// <summary>
    /// Determines if the Next button should be enabled on Step 3
    /// </summary>
    public static bool IsStep3SelectServicesNextEnabled(SessionPrepWizardState state)
    {
        // At least one service must be selected
        return state.UseLocalFiles || state.UseYouTube || state.UseKarafun;
    }
    
    /// <summary>
    /// Determines if Step 4 can auto-continue (all checks passed)
    /// </summary>
    public static bool CanAutoAdvanceFromStep4PrepareSession(SessionPrepWizardState state)
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
    /// Determines if Step 5 Next button should be enabled
    /// </summary>
    public static bool IsStep5LaunchKarafunNextEnabled(SessionPrepWizardState state)
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
    /// Determines if Step 6 Next button should be enabled
    /// </summary>
    public static bool IsStep6KarafunRemoteControlNextEnabled(SessionPrepWizardState state)
    {
        return state.KarafunRemoteConnected;
    }
    
    /// <summary>
    /// Gets the initial tab to navigate to after wizard completion
    /// </summary>
    public static SessionPrepWizardFinishDestination GetWizardDestination(SessionPrepWizardState state)
    {
        // If local files is selected but no songs are scanned, go to Local Files tab
        if (state.UseLocalFiles && state.LocalSongCount == 0)
        {
            return SessionPrepWizardFinishDestination.LocalFilesTab;
        }
        
        return SessionPrepWizardFinishDestination.SearchTab;
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

public enum SessionPrepWizardFinishDestination
{
    SearchTab,
    LocalFilesTab
}
