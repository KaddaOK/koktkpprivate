using KOKTKaraokeParty.Controls.SessionPrepWizard;
using Xunit;
using System.Collections.Generic;

namespace KOKTKaraokeParty.Tests.Controls;

public class SessionPrepWizardLogicTests
{
    #region ShouldShowRestoreQueueStep Tests
    
    [Fact]
    public void ShouldShowRestoreQueueStep_WithSavedQueue_ReturnsTrue()
    {
        var state = new SessionPrepWizardState
        {
            HasSavedQueue = true,
            SavedQueueItems = new List<SavedQueueItemInfo>
            {
                new() { SingerName = "Test", SongName = "Song", ItemType = ItemType.LocalMp4 }
            }
        };
        
        Assert.True(SessionPrepWizardLogic.ShouldShowRestoreQueueStep(state));
    }
    
    [Fact]
    public void ShouldShowRestoreQueueStep_WithEmptyQueue_ReturnsFalse()
    {
        var state = new SessionPrepWizardState
        {
            HasSavedQueue = true,
            SavedQueueItems = new List<SavedQueueItemInfo>()
        };
        
        Assert.False(SessionPrepWizardLogic.ShouldShowRestoreQueueStep(state));
    }
    
    [Fact]
    public void ShouldShowRestoreQueueStep_WithNoSavedQueue_ReturnsFalse()
    {
        var state = new SessionPrepWizardState
        {
            HasSavedQueue = false,
            SavedQueueItems = new List<SavedQueueItemInfo>
            {
                new() { SingerName = "Test", SongName = "Song", ItemType = ItemType.LocalMp4 }
            }
        };
        
        Assert.False(SessionPrepWizardLogic.ShouldShowRestoreQueueStep(state));
    }
    
    #endregion
    
    #region GetRequiredServicesFromQueue Tests
    
    [Fact]
    public void GetRequiredServicesFromQueue_StartFresh_ReturnsNoRequirements()
    {
        var items = new List<SavedQueueItemInfo>
        {
            new() { ItemType = ItemType.LocalMp4 },
            new() { ItemType = ItemType.Youtube },
            new() { ItemType = ItemType.KarafunWeb }
        };
        
        var result = SessionPrepWizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.StartFresh);
        
        Assert.False(result.RequiresLocalFiles);
        Assert.False(result.RequiresYouTube);
        Assert.False(result.RequiresKarafun);
    }
    
    [Fact]
    public void GetRequiredServicesFromQueue_YesAll_ReturnsAllRequirements()
    {
        var items = new List<SavedQueueItemInfo>
        {
            new() { ItemType = ItemType.LocalMp4 },
            new() { ItemType = ItemType.Youtube },
            new() { ItemType = ItemType.KarafunWeb }
        };
        
        var result = SessionPrepWizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesAll);
        
        Assert.True(result.RequiresLocalFiles);
        Assert.True(result.RequiresYouTube);
        Assert.True(result.RequiresKarafun);
    }
    
    [Fact]
    public void GetRequiredServicesFromQueue_YesExceptFirst_SkipsFirstItem()
    {
        var items = new List<SavedQueueItemInfo>
        {
            new() { ItemType = ItemType.KarafunWeb },  // First item, should be skipped
            new() { ItemType = ItemType.Youtube },
            new() { ItemType = ItemType.LocalMp3G }
        };
        
        var result = SessionPrepWizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesExceptFirst);
        
        Assert.True(result.RequiresLocalFiles);
        Assert.True(result.RequiresYouTube);
        Assert.False(result.RequiresKarafun);  // First item was Karafun, should be skipped
    }
    
    [Fact]
    public void GetRequiredServicesFromQueue_LocalMp3G_RequiresLocalFiles()
    {
        var items = new List<SavedQueueItemInfo>
        {
            new() { ItemType = ItemType.LocalMp3G }
        };
        
        var result = SessionPrepWizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesAll);
        
        Assert.True(result.RequiresLocalFiles);
        Assert.False(result.RequiresYouTube);
        Assert.False(result.RequiresKarafun);
    }
    
    [Fact]
    public void GetRequiredServicesFromQueue_LocalMp3GZip_RequiresLocalFiles()
    {
        var items = new List<SavedQueueItemInfo>
        {
            new() { ItemType = ItemType.LocalMp3GZip }
        };
        
        var result = SessionPrepWizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesAll);
        
        Assert.True(result.RequiresLocalFiles);
    }
    
    #endregion
    
    #region Service Preparation Need Tests
    
    [Fact]
    public void NeedsVlcPrepare_LocalFilesEnabled_ReturnsTrue()
    {
        var state = new SessionPrepWizardState { UseLocalFiles = true, UseYouTube = false };
        
        Assert.True(SessionPrepWizardLogic.NeedsVlcPrepare(state));
    }
    
    [Fact]
    public void NeedsVlcPrepare_YouTubeEnabled_ReturnsTrue()
    {
        var state = new SessionPrepWizardState { UseLocalFiles = false, UseYouTube = true };
        
        Assert.True(SessionPrepWizardLogic.NeedsVlcPrepare(state));
    }
    
    [Fact]
    public void NeedsVlcPrepare_BothDisabled_ReturnsFalse()
    {
        var state = new SessionPrepWizardState { UseLocalFiles = false, UseYouTube = false };
        
        Assert.False(SessionPrepWizardLogic.NeedsVlcPrepare(state));
    }
    
    [Fact]
    public void NeedsYtDlpPrepare_YouTubeEnabled_ReturnsTrue()
    {
        var state = new SessionPrepWizardState { UseYouTube = true };
        
        Assert.True(SessionPrepWizardLogic.NeedsYtDlpPrepare(state));
    }
    
    [Fact]
    public void NeedsYtDlpPrepare_YouTubeDisabled_ReturnsFalse()
    {
        var state = new SessionPrepWizardState { UseYouTube = false };
        
        Assert.False(SessionPrepWizardLogic.NeedsYtDlpPrepare(state));
    }
    
    [Fact]
    public void NeedsBrowserCheck_KarafunWithControlledBrowser_ReturnsTrue()
    {
        var state = new SessionPrepWizardState 
        { 
            UseKarafun = true, 
            KarafunMode = KarafunMode.ControlledBrowser 
        };
        
        Assert.True(SessionPrepWizardLogic.NeedsBrowserCheck(state));
    }
    
    [Fact]
    public void NeedsBrowserCheck_KarafunWithInstalledApp_ReturnsFalse()
    {
        var state = new SessionPrepWizardState 
        { 
            UseKarafun = true, 
            KarafunMode = KarafunMode.InstalledApp 
        };
        
        Assert.False(SessionPrepWizardLogic.NeedsBrowserCheck(state));
    }
    
    [Fact]
    public void NeedsBrowserCheck_KarafunDisabled_ReturnsFalse()
    {
        var state = new SessionPrepWizardState 
        { 
            UseKarafun = false, 
            KarafunMode = KarafunMode.ControlledBrowser 
        };
        
        Assert.False(SessionPrepWizardLogic.NeedsBrowserCheck(state));
    }
    
    #endregion
    
    #region Step Navigation Tests
    
    [Fact]
    public void ShouldShowKarafunSteps_KarafunEnabled_ReturnsTrue()
    {
        var state = new SessionPrepWizardState { UseKarafun = true };
        
        Assert.True(SessionPrepWizardLogic.ShouldShowKarafunSteps(state));
    }
    
    [Fact]
    public void ShouldShowKarafunSteps_KarafunDisabled_ReturnsFalse()
    {
        var state = new SessionPrepWizardState { UseKarafun = false };
        
        Assert.False(SessionPrepWizardLogic.ShouldShowKarafunSteps(state));
    }
    
    [Fact]
    public void IsStep3SelectServicesNextEnabled_NoServicesSelected_ReturnsFalse()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = false, 
            UseYouTube = false, 
            UseKarafun = false 
        };
        
        Assert.False(SessionPrepWizardLogic.IsStep3SelectServicesNextEnabled(state));
    }
    
    [Fact]
    public void IsStep3SelectServicesNextEnabled_AtLeastOneServiceSelected_ReturnsTrue()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = false, 
            UseYouTube = true, 
            UseKarafun = false 
        };
        
        Assert.True(SessionPrepWizardLogic.IsStep3SelectServicesNextEnabled(state));
    }
    
    #endregion
    
    #region Step 4 Auto-Advance Tests
    
    [Fact]
    public void CanAutoAdvanceFromStep4PrepareSession_AllRequiredReady_ReturnsTrue()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = true, 
            UseYouTube = true,
            UseKarafun = true,
            KarafunMode = KarafunMode.ControlledBrowser,
            VlcReady = true,
            YtDlpReady = true,
            BrowserReady = true
        };
        
        Assert.True(SessionPrepWizardLogic.CanAutoAdvanceFromStep4PrepareSession(state));
    }
    
    [Fact]
    public void CanAutoAdvanceFromStep4PrepareSession_VlcNotReady_ReturnsFalse()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = true, 
            VlcReady = false
        };
        
        Assert.False(SessionPrepWizardLogic.CanAutoAdvanceFromStep4PrepareSession(state));
    }
    
    [Fact]
    public void CanAutoAdvanceFromStep4PrepareSession_NoServicesNeeded_ReturnsTrue()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = false, 
            UseYouTube = false,
            UseKarafun = false
        };
        
        // Even with nothing ready, if nothing is needed, it should pass
        Assert.True(SessionPrepWizardLogic.CanAutoAdvanceFromStep4PrepareSession(state));
    }
    
    #endregion
    
    #region Step 5/6 Enable Tests
    
    [Fact]
    public void IsStep5LaunchKarafunNextEnabled_ControlledBrowserNotLaunched_ReturnsFalse()
    {
        var state = new SessionPrepWizardState 
        { 
            KarafunMode = KarafunMode.ControlledBrowser,
            KarafunWebPlayerLaunched = false
        };
        
        Assert.False(SessionPrepWizardLogic.IsStep5LaunchKarafunNextEnabled(state));
    }
    
    [Fact]
    public void IsStep5LaunchKarafunNextEnabled_ControlledBrowserLaunched_ReturnsTrue()
    {
        var state = new SessionPrepWizardState 
        { 
            KarafunMode = KarafunMode.ControlledBrowser,
            KarafunWebPlayerLaunched = true
        };
        
        Assert.True(SessionPrepWizardLogic.IsStep5LaunchKarafunNextEnabled(state));
    }
    
    [Fact]
    public void IsStep5LaunchKarafunNextEnabled_InstalledApp_AlwaysReturnsTrue()
    {
        var state = new SessionPrepWizardState 
        { 
            KarafunMode = KarafunMode.InstalledApp,
            KarafunWebPlayerLaunched = false  // Doesn't matter for installed app
        };
        
        Assert.True(SessionPrepWizardLogic.IsStep5LaunchKarafunNextEnabled(state));
    }
    
    [Fact]
    public void IsStep6KarafunRemoteControlNextEnabled_NotConnected_ReturnsFalse()
    {
        var state = new SessionPrepWizardState { KarafunRemoteConnected = false };
        
        Assert.False(SessionPrepWizardLogic.IsStep6KarafunRemoteControlNextEnabled(state));
    }
    
    [Fact]
    public void IsStep6KarafunRemoteControlNextEnabled_Connected_ReturnsTrue()
    {
        var state = new SessionPrepWizardState { KarafunRemoteConnected = true };
        
        Assert.True(SessionPrepWizardLogic.IsStep6KarafunRemoteControlNextEnabled(state));
    }
    
    #endregion
    
    #region Wizard Destination Tests
    
    [Fact]
    public void GetWizardDestination_LocalFilesWithNoSongs_ReturnsLocalFilesTab()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = true, 
            LocalSongCount = 0 
        };
        
        Assert.Equal(SessionPrepWizardFinishDestination.LocalFilesTab, SessionPrepWizardLogic.GetWizardDestination(state));
    }
    
    [Fact]
    public void GetWizardDestination_LocalFilesWithSongs_ReturnsSearchTab()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = true, 
            LocalSongCount = 100 
        };
        
        Assert.Equal(SessionPrepWizardFinishDestination.SearchTab, SessionPrepWizardLogic.GetWizardDestination(state));
    }
    
    [Fact]
    public void GetWizardDestination_NoLocalFiles_ReturnsSearchTab()
    {
        var state = new SessionPrepWizardState 
        { 
            UseLocalFiles = false, 
            LocalSongCount = 0 
        };
        
        Assert.Equal(SessionPrepWizardFinishDestination.SearchTab, SessionPrepWizardLogic.GetWizardDestination(state));
    }
    
    #endregion
    
    #region Room Code Validation Tests
    
    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", true)]
    [InlineData("999999", true)]
    [InlineData("12345", false)]    // Too short
    [InlineData("1234567", false)]  // Too long
    [InlineData("12345a", false)]   // Contains letter
    [InlineData("", false)]         // Empty
    [InlineData("  123456  ", false)] // With spaces
    public void IsValidRoomCodeFormat_VariousCodes_ReturnsExpected(string code, bool expected)
    {
        Assert.Equal(expected, SessionPrepWizardLogic.IsValidRoomCodeFormat(code));
    }
    
    #endregion
}
