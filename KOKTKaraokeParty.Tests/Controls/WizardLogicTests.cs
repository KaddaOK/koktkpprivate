using KOKTKaraokeParty.Controls.SessionPrepWizard;
using Xunit;
using System.Collections.Generic;

namespace KOKTKaraokeParty.Tests.Controls;

public class WizardLogicTests
{
    #region ShouldShowRestoreQueueStep Tests
    
    [Fact]
    public void ShouldShowRestoreQueueStep_WithSavedQueue_ReturnsTrue()
    {
        var state = new WizardState
        {
            HasSavedQueue = true,
            SavedQueueItems = new List<SavedQueueItemInfo>
            {
                new() { SingerName = "Test", SongName = "Song", ItemType = ItemType.LocalMp4 }
            }
        };
        
        Assert.True(WizardLogic.ShouldShowRestoreQueueStep(state));
    }
    
    [Fact]
    public void ShouldShowRestoreQueueStep_WithEmptyQueue_ReturnsFalse()
    {
        var state = new WizardState
        {
            HasSavedQueue = true,
            SavedQueueItems = new List<SavedQueueItemInfo>()
        };
        
        Assert.False(WizardLogic.ShouldShowRestoreQueueStep(state));
    }
    
    [Fact]
    public void ShouldShowRestoreQueueStep_WithNoSavedQueue_ReturnsFalse()
    {
        var state = new WizardState
        {
            HasSavedQueue = false,
            SavedQueueItems = new List<SavedQueueItemInfo>
            {
                new() { SingerName = "Test", SongName = "Song", ItemType = ItemType.LocalMp4 }
            }
        };
        
        Assert.False(WizardLogic.ShouldShowRestoreQueueStep(state));
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
        
        var result = WizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.StartFresh);
        
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
        
        var result = WizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesAll);
        
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
        
        var result = WizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesExceptFirst);
        
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
        
        var result = WizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesAll);
        
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
        
        var result = WizardLogic.GetRequiredServicesFromQueue(items, QueueRestoreOption.YesAll);
        
        Assert.True(result.RequiresLocalFiles);
    }
    
    #endregion
    
    #region Service Preparation Need Tests
    
    [Fact]
    public void NeedsVlcPrepare_LocalFilesEnabled_ReturnsTrue()
    {
        var state = new WizardState { UseLocalFiles = true, UseYouTube = false };
        
        Assert.True(WizardLogic.NeedsVlcPrepare(state));
    }
    
    [Fact]
    public void NeedsVlcPrepare_YouTubeEnabled_ReturnsTrue()
    {
        var state = new WizardState { UseLocalFiles = false, UseYouTube = true };
        
        Assert.True(WizardLogic.NeedsVlcPrepare(state));
    }
    
    [Fact]
    public void NeedsVlcPrepare_BothDisabled_ReturnsFalse()
    {
        var state = new WizardState { UseLocalFiles = false, UseYouTube = false };
        
        Assert.False(WizardLogic.NeedsVlcPrepare(state));
    }
    
    [Fact]
    public void NeedsYtDlpPrepare_YouTubeEnabled_ReturnsTrue()
    {
        var state = new WizardState { UseYouTube = true };
        
        Assert.True(WizardLogic.NeedsYtDlpPrepare(state));
    }
    
    [Fact]
    public void NeedsYtDlpPrepare_YouTubeDisabled_ReturnsFalse()
    {
        var state = new WizardState { UseYouTube = false };
        
        Assert.False(WizardLogic.NeedsYtDlpPrepare(state));
    }
    
    [Fact]
    public void NeedsBrowserCheck_KarafunWithControlledBrowser_ReturnsTrue()
    {
        var state = new WizardState 
        { 
            UseKarafun = true, 
            KarafunMode = KarafunMode.ControlledBrowser 
        };
        
        Assert.True(WizardLogic.NeedsBrowserCheck(state));
    }
    
    [Fact]
    public void NeedsBrowserCheck_KarafunWithInstalledApp_ReturnsFalse()
    {
        var state = new WizardState 
        { 
            UseKarafun = true, 
            KarafunMode = KarafunMode.InstalledApp 
        };
        
        Assert.False(WizardLogic.NeedsBrowserCheck(state));
    }
    
    [Fact]
    public void NeedsBrowserCheck_KarafunDisabled_ReturnsFalse()
    {
        var state = new WizardState 
        { 
            UseKarafun = false, 
            KarafunMode = KarafunMode.ControlledBrowser 
        };
        
        Assert.False(WizardLogic.NeedsBrowserCheck(state));
    }
    
    #endregion
    
    #region Step Navigation Tests
    
    [Fact]
    public void ShouldShowKarafunSteps_KarafunEnabled_ReturnsTrue()
    {
        var state = new WizardState { UseKarafun = true };
        
        Assert.True(WizardLogic.ShouldShowKarafunSteps(state));
    }
    
    [Fact]
    public void ShouldShowKarafunSteps_KarafunDisabled_ReturnsFalse()
    {
        var state = new WizardState { UseKarafun = false };
        
        Assert.False(WizardLogic.ShouldShowKarafunSteps(state));
    }
    
    [Fact]
    public void IsStepCNextEnabled_NoServicesSelected_ReturnsFalse()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = false, 
            UseYouTube = false, 
            UseKarafun = false 
        };
        
        Assert.False(WizardLogic.IsStepCNextEnabled(state));
    }
    
    [Fact]
    public void IsStepCNextEnabled_AtLeastOneServiceSelected_ReturnsTrue()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = false, 
            UseYouTube = true, 
            UseKarafun = false 
        };
        
        Assert.True(WizardLogic.IsStepCNextEnabled(state));
    }
    
    #endregion
    
    #region Step D Auto-Advance Tests
    
    [Fact]
    public void CanAutoAdvanceFromStepD_AllRequiredReady_ReturnsTrue()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = true, 
            UseYouTube = true,
            UseKarafun = true,
            KarafunMode = KarafunMode.ControlledBrowser,
            VlcReady = true,
            YtDlpReady = true,
            BrowserReady = true
        };
        
        Assert.True(WizardLogic.CanAutoAdvanceFromStepD(state));
    }
    
    [Fact]
    public void CanAutoAdvanceFromStepD_VlcNotReady_ReturnsFalse()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = true, 
            VlcReady = false
        };
        
        Assert.False(WizardLogic.CanAutoAdvanceFromStepD(state));
    }
    
    [Fact]
    public void CanAutoAdvanceFromStepD_NoServicesNeeded_ReturnsTrue()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = false, 
            UseYouTube = false,
            UseKarafun = false
        };
        
        // Even with nothing ready, if nothing is needed, it should pass
        Assert.True(WizardLogic.CanAutoAdvanceFromStepD(state));
    }
    
    #endregion
    
    #region Step E/F Enable Tests
    
    [Fact]
    public void IsStepENextEnabled_ControlledBrowserNotLaunched_ReturnsFalse()
    {
        var state = new WizardState 
        { 
            KarafunMode = KarafunMode.ControlledBrowser,
            KarafunWebPlayerLaunched = false
        };
        
        Assert.False(WizardLogic.IsStepENextEnabled(state));
    }
    
    [Fact]
    public void IsStepENextEnabled_ControlledBrowserLaunched_ReturnsTrue()
    {
        var state = new WizardState 
        { 
            KarafunMode = KarafunMode.ControlledBrowser,
            KarafunWebPlayerLaunched = true
        };
        
        Assert.True(WizardLogic.IsStepENextEnabled(state));
    }
    
    [Fact]
    public void IsStepENextEnabled_InstalledApp_AlwaysReturnsTrue()
    {
        var state = new WizardState 
        { 
            KarafunMode = KarafunMode.InstalledApp,
            KarafunWebPlayerLaunched = false  // Doesn't matter for installed app
        };
        
        Assert.True(WizardLogic.IsStepENextEnabled(state));
    }
    
    [Fact]
    public void IsStepFNextEnabled_NotConnected_ReturnsFalse()
    {
        var state = new WizardState { KarafunRemoteConnected = false };
        
        Assert.False(WizardLogic.IsStepFNextEnabled(state));
    }
    
    [Fact]
    public void IsStepFNextEnabled_Connected_ReturnsTrue()
    {
        var state = new WizardState { KarafunRemoteConnected = true };
        
        Assert.True(WizardLogic.IsStepFNextEnabled(state));
    }
    
    #endregion
    
    #region Wizard Destination Tests
    
    [Fact]
    public void GetWizardDestination_LocalFilesWithNoSongs_ReturnsLocalFilesTab()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = true, 
            LocalSongCount = 0 
        };
        
        Assert.Equal(WizardDestination.LocalFilesTab, WizardLogic.GetWizardDestination(state));
    }
    
    [Fact]
    public void GetWizardDestination_LocalFilesWithSongs_ReturnsSearchTab()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = true, 
            LocalSongCount = 100 
        };
        
        Assert.Equal(WizardDestination.SearchTab, WizardLogic.GetWizardDestination(state));
    }
    
    [Fact]
    public void GetWizardDestination_NoLocalFiles_ReturnsSearchTab()
    {
        var state = new WizardState 
        { 
            UseLocalFiles = false, 
            LocalSongCount = 0 
        };
        
        Assert.Equal(WizardDestination.SearchTab, WizardLogic.GetWizardDestination(state));
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
        Assert.Equal(expected, WizardLogic.IsValidRoomCodeFormat(code));
    }
    
    #endregion
}
