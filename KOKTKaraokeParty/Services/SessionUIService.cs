using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;

namespace KOKTKaraokeParty.Services;

[Meta(typeof(IAutoNode))]
public partial class SessionUIService : Node
{
    private SessionPreparationService _sessionPreparation;

    public void Initialize(SessionPreparationService sessionPreparation)
    {
        _sessionPreparation = sessionPreparation;
    }

    public void OnReady()
    {
        this.Provide();
    }

    public void PopulateSessionTree(ITree tree, PrepareSessionModel model, 
        IButton okButton, IButton loginButton)
    {
        Callable.From(() =>
        {
            var infoItemsFontSize = 12;
            tree.Columns = 2;
            tree.SetColumnCustomMinimumWidth(0, 50);
            tree.SetColumnExpand(0, false);
            tree.SetColumnTitlesVisible(false);
            tree.HideRoot = true;
            tree.HideFolding = true;
            tree.Clear();
            var treeRoot = tree.CreateItem();

            // Streamed Content Section
            var streamedContentItem = treeRoot.CreateChild();
            streamedContentItem.SetText(0, _sessionPreparation.GetStreamedContentIcon(model));
            streamedContentItem.SetText(1, "Prepare to Stream Content");

            AddYtDlpTreeItems(streamedContentItem, model, infoItemsFontSize);
            AddBrowserTreeItems(streamedContentItem, model, infoItemsFontSize);
            AddYoutubeTreeItems(streamedContentItem, model, infoItemsFontSize);
            AddKarafunTreeItems(streamedContentItem, model, infoItemsFontSize);

            // Local Content Section
            var localItem = treeRoot.CreateChild();
            var localContentIcon = GetVLCStatusIcon(model.VLCStatus);
            localItem.SetText(0, localContentIcon);
            localItem.SetText(1, "Prepare for Local File Content");
            
            AddVlcTreeItems(localItem, model, infoItemsFontSize);

            // Update button states
            UpdateButtonStates(model, okButton, loginButton);
        }).CallDeferred();
    }

    private void AddYtDlpTreeItems(TreeItem parent, PrepareSessionModel model, int fontSize)
    {
        var ytDlpItem = parent.CreateChild();
        var ytDlpStatusStrings = _sessionPreparation.GetYtDlpStatusLine(model.YtDlpStatus);
        ytDlpItem.SetText(0, ytDlpStatusStrings.icon);
        ytDlpItem.SetText(1, $"yt-dlp: {ytDlpStatusStrings.description}");
        
        if (!string.IsNullOrEmpty(model.YtDlpIdentity))
        {
            var identityItem = ytDlpItem.CreateChild();
            identityItem.SetText(1, model.YtDlpIdentity);
            identityItem.SetCustomFontSize(0, fontSize);
            identityItem.SetCustomFontSize(1, fontSize);
        }
        
        if (!string.IsNullOrEmpty(model.YtDlpMessage))
        {
            var messageItem = ytDlpItem.CreateChild();
            messageItem.SetText(1, model.YtDlpMessage);
            messageItem.SetCustomFontSize(0, fontSize);
            messageItem.SetCustomFontSize(1, fontSize);
        }
    }

    private void AddBrowserTreeItems(TreeItem parent, PrepareSessionModel model, int fontSize)
    {
        var browserItem = parent.CreateChild();
        var browserStatusStrings = _sessionPreparation.GetBrowserStatusLine(model.BrowserStatus);
        browserItem.SetText(0, browserStatusStrings.icon);
        browserItem.SetText(1, $"Browser: {browserStatusStrings.description}");
        
        if (!string.IsNullOrEmpty(model.BrowserIdentity) && model.BrowserStatus != BrowserAvailabilityStatus.Busy)
        {
            var identityItem = browserItem.CreateChild();
            identityItem.SetText(1, model.BrowserIdentity);
            identityItem.SetCustomFontSize(0, fontSize);
            identityItem.SetCustomFontSize(1, fontSize);
        }
        
        if (!string.IsNullOrEmpty(model.BrowserMessage))
        {
            var messageItem = browserItem.CreateChild();
            messageItem.SetText(1, model.BrowserMessage);
            messageItem.SetCustomFontSize(0, fontSize);
            messageItem.SetCustomFontSize(1, fontSize);
        }
    }

    private void AddYoutubeTreeItems(TreeItem parent, PrepareSessionModel model, int fontSize)
    {
        var youtubeItem = parent.CreateChild();
        var youtubeStatusStrings = _sessionPreparation.GetYouTubeStatusLine(model.YouTubeStatus);
        youtubeItem.SetText(0, youtubeStatusStrings.icon);
        youtubeItem.SetText(1, $"YouTube: {youtubeStatusStrings.description}");
        
        if (!string.IsNullOrEmpty(model.YouTubeIdentity))
        {
            var identityItem = youtubeItem.CreateChild();
            identityItem.SetText(1, model.YouTubeIdentity);
            identityItem.SetCustomFontSize(0, fontSize);
            identityItem.SetCustomFontSize(1, fontSize);
        }
        
        if (!string.IsNullOrEmpty(model.YouTubeMessage))
        {
            var messageItem = youtubeItem.CreateChild();
            messageItem.SetText(1, model.YouTubeMessage);
            messageItem.SetCustomFontSize(0, fontSize);
            messageItem.SetCustomFontSize(1, fontSize);
        }
    }

    private void AddKarafunTreeItems(TreeItem parent, PrepareSessionModel model, int fontSize)
    {
        var karafunItem = parent.CreateChild();
        var karafunStatusStrings = _sessionPreparation.GetKarafunStatusLine(model.KarafunStatus);
        karafunItem.SetText(0, karafunStatusStrings.icon);
        karafunItem.SetText(1, $"Karafun: {karafunStatusStrings.description}");
        
        if (!string.IsNullOrEmpty(model.KarafunIdentity))
        {
            var identityItem = karafunItem.CreateChild();
            identityItem.SetText(1, model.KarafunIdentity);
            identityItem.SetCustomFontSize(0, fontSize);
            identityItem.SetCustomFontSize(1, fontSize);
        }
        
        if (!string.IsNullOrEmpty(model.KarafunMessage))
        {
            var messageItem = karafunItem.CreateChild();
            messageItem.SetText(1, model.KarafunMessage);
            messageItem.SetCustomFontSize(0, fontSize);
            messageItem.SetCustomFontSize(1, fontSize);
        }
    }

    private void AddVlcTreeItems(TreeItem parent, PrepareSessionModel model, int fontSize)
    {
        var vlcItem = parent.CreateChild();
        var vlcStatusIcon = GetVLCStatusIcon(model.VLCStatus);
        var vlcStatusText = GetVLCStatusText(model.VLCStatus);
        
        vlcItem.SetText(0, vlcStatusIcon);
        vlcItem.SetText(1, vlcStatusText);
        
        if (!string.IsNullOrEmpty(model.VLCMessage))
        {
            var messageItem = vlcItem.CreateChild();
            messageItem.SetText(1, model.VLCMessage);
            messageItem.SetCustomFontSize(0, fontSize);
            messageItem.SetCustomFontSize(1, fontSize);
        }
    }

    private string GetVLCStatusIcon(VLCStatus status)
    {
        return status switch
        {
            VLCStatus.NotStarted => "⬛",
            VLCStatus.Initializing => "⏳",
            VLCStatus.Ready => "✔",
            VLCStatus.FatalError => "❌",
            _ => "⚠"
        };
    }

    private string GetVLCStatusText(VLCStatus status)
    {
        return status switch
        {
            VLCStatus.Initializing => "Loading VLC libraries...",
            VLCStatus.Ready => "Ready to play back video",
            VLCStatus.FatalError => "Error",
            _ => "Unknown"
        };
    }

    private void UpdateButtonStates(PrepareSessionModel model, IButton okButton, IButton loginButton)
    {
        var checksAreStillInProgress = IsAnyCheckInProgress(model);
        var nothingIsUsable = AreAllServicesUnusable(model);

        loginButton.Disabled = checksAreStillInProgress;

        if (checksAreStillInProgress)
        {
            okButton.Disabled = true;
            okButton.TooltipText = "Please wait until all checks are complete.";
            okButton.Text = "Preparing...";
        }
        else if (nothingIsUsable)
        {
            okButton.Disabled = true;
            okButton.TooltipText = "No usage options passed checks.";
            okButton.Text = "Failed";
        }
        else
        {
            okButton.Disabled = false;
            okButton.Text = "Start the Party!";
        }
    }

    private bool IsAnyCheckInProgress(PrepareSessionModel model)
    {
        return model.BrowserStatus == BrowserAvailabilityStatus.NotStarted ||
               model.BrowserStatus == BrowserAvailabilityStatus.Checking ||
               model.BrowserStatus == BrowserAvailabilityStatus.Downloading ||
               model.YouTubeStatus == YouTubeStatus.NotStarted ||
               model.YouTubeStatus == YouTubeStatus.Checking ||
               model.KarafunStatus == KarafunStatus.NotStarted ||
               model.KarafunStatus == KarafunStatus.Checking ||
               model.VLCStatus == VLCStatus.NotStarted ||
               model.VLCStatus == VLCStatus.Initializing ||
               model.YtDlpStatus == YtDlpStatus.NotStarted ||
               model.YtDlpStatus == YtDlpStatus.Checking ||
               model.YtDlpStatus == YtDlpStatus.Downloading;
    }

    private bool AreAllServicesUnusable(PrepareSessionModel model)
    {
        var noLocalPlayback = model.VLCStatus != VLCStatus.Ready;
        var noYoutubePlayback = model.BrowserStatus != BrowserAvailabilityStatus.Ready ||
                               model.YouTubeStatus == YouTubeStatus.FatalError;
        var noKarafunPlayback = model.BrowserStatus != BrowserAvailabilityStatus.Ready ||
                               model.KarafunStatus != KarafunStatus.Active;

        return noLocalPlayback && noYoutubePlayback && noKarafunPlayback;
    }
}