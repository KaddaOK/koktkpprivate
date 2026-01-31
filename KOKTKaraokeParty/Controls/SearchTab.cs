using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface ISearchTab : IMarginContainer
{
    void ExternalFileShowAddDialog(QueueItem item);
    void ConfigureAvailableServices(bool localFilesAvailable, bool youTubeAvailable, bool karafunAvailable);
    event SearchTab.ItemAddedToQueueEventHandler ItemAddedToQueue;
}

[Meta(typeof(IAutoNode))]
public partial class SearchTab : MarginContainer, ISearchTab
{
    public override void _Notification(int what) => this.Notify(what);

    #region Dependencies
    
    [Dependency] private Settings Settings => this.DependOn<Settings>();
    
    #endregion

    private List<KarafunApiSong> KarafunApiResults;
    private List<KNSearchResultItem> KNResults;
    private List<LocalSongFileEntry> LocalFilesResults;
    private bool isStreamingKfnResults = false;

    private QueueItem itemBeingAdded;

    private bool isAddToQueueResolvingPerformLink = false;

    private TreeItem _kfnRoot;
    private TreeItem _knRoot;
    private TreeItem _localFilesRoot;

    private CancellationTokenSource SearchCancellationSource = new CancellationTokenSource();

    private ILocalSearcher _localSearcher = new LocalSearcher(); // TODO: inject properly


    #region Nodes

    [Node] private ConfirmationDialog AddToQueueDialog { get; set; } = default!;
    [Node] private LineEdit EnterSingerName { get; set; } = default!;
    [Node] private Tree KfnResultsTree { get; set; } = default!;
    [Node] private ILoadableLabel KfnResultCount { get; set; } = default!;
    [Node] private Tree KNResultsTree { get; set; } = default!;
    [Node] private ILoadableLabel KNResultCount { get; set; } = default!;
    [Node] private Tree LocalFilesResultsTree { get; set; } = default!;
    [Node] private ILoadableLabel LocalFilesResultCount { get; set; } = default!;
    [Node] private LineEdit SearchText { get; set; } = default!;
    [Node] private Button SearchButton { get; set; } = default!;
    [Node] private Button ClearSearchButton { get; set; } = default!;
    [Node] private ILoadableLabel QueueAddSongNameLabel { get; set; } = default!;
    [Node] private ILoadableLabel QueueAddArtistNameLabel { get; set; } = default!;
    [Node] private Label QueueAddCreatorNameLabel { get; set; } = default!;
    [Node] private ICheckBox SearchKarafunCheckBox { get; set; } = default!;
    [Node] private ICheckBox SearchKaraokeNerdsCheckBox { get; set; } = default!;
    [Node] private ICheckBox SearchLocalFilesCheckBox { get; set; } = default!;
    [Node] private IVBoxContainer KarafunResultsVBox { get; set; } = default!;
    [Node] private IVBoxContainer KaraokeNerdsResultsVBox { get; set; } = default!;
    [Node] private IVBoxContainer LocalResultsPane { get; set; } = default!;
    [Node] private IHSplitContainer WebResultsHSplitContainer { get; set; } = default!;

    #endregion

    #region Signals

    [Signal]
    public delegate void ItemAddedToQueueEventHandler(QueueItem itemBeingAdded);

    #endregion

    public void OnReady()
    {
        SetupKfnTree();
        SetupKNTree();
        SetupLocalFilesTree();

        SearchText.TextSubmitted += Search;
        SearchButton.Pressed += () => Search(SearchText.Text);
        ClearSearchButton.Pressed += ClearSearch;

        EnterSingerName.TextChanged += (_) => DisableOrEnableAddToQueueOkButton();
        EnterSingerName.TextSubmitted += (_) => AddToQueueDialogConfirmed();
        AddToQueueDialog.Confirmed += AddToQueueDialogConfirmed;
        AddToQueueDialog.Canceled += CloseAddToQueueDialog;

        // Connect checkbox events to control visibility
        SearchKarafunCheckBox.Toggled += (_) => UpdateResultPaneVisibility();
        SearchKaraokeNerdsCheckBox.Toggled += (_) => UpdateResultPaneVisibility();
        SearchLocalFilesCheckBox.Toggled += (_) => UpdateResultPaneVisibility();

        // Initial visibility update
        UpdateResultPaneVisibility();

        KfnResultCount.SetLoaded(true, "");
        KNResultCount.SetLoaded(true, "");
        LocalFilesResultCount.SetLoaded(true, "");
    }


    private void UpdateResultPaneVisibility()
    {
        KarafunResultsVBox.Visible = SearchKarafunCheckBox.ButtonPressed;
        KaraokeNerdsResultsVBox.Visible = SearchKaraokeNerdsCheckBox.ButtonPressed;
        LocalResultsPane.Visible = SearchLocalFilesCheckBox.ButtonPressed;
        
        // Hide the WebResultsHSplitContainer entirely if both its children are hidden
        WebResultsHSplitContainer.Visible = SearchKarafunCheckBox.ButtonPressed || SearchKaraokeNerdsCheckBox.ButtonPressed;
    }

    public void ConfigureAvailableServices(bool localFilesAvailable, bool youTubeAvailable, bool karafunAvailable)
    {
        SearchLocalFilesCheckBox.ButtonPressed = localFilesAvailable;
        SearchLocalFilesCheckBox.Disabled = !localFilesAvailable;
        
        SearchKaraokeNerdsCheckBox.ButtonPressed = youTubeAvailable;
        SearchKaraokeNerdsCheckBox.Disabled = !youTubeAvailable;
        
        SearchKarafunCheckBox.ButtonPressed = karafunAvailable;
        SearchKarafunCheckBox.Disabled = !karafunAvailable;
        
        UpdateResultPaneVisibility();
    }

    private void ClearSearch()
    {
        SearchCancellationSource.Cancel();
        SearchText.Text = "";
        KfnResultsTree.Clear();
        KfnResultCount.SetLoaded(true, "");
        KNResultsTree.Clear();
        KNResultCount.SetLoaded(true, "");
        LocalFilesResultsTree.Clear();
        LocalFilesResultCount.SetLoaded(true, "");
        SearchText.GrabFocus();
    }

    public void ExternalFileShowAddDialog(QueueItem item)
    {
        itemBeingAdded = item;
        ShowAddToQueueDialog(item.CreatorName, true, item.SongName, item.ArtistName);
    }

    private void SetupKfnTree()
    {
        KfnResultsTree.Columns = 2;
        KfnResultsTree.SetColumnTitle(0, "Song Name");
        KfnResultsTree.SetColumnTitle(1, "Artist Name");
        KfnResultsTree.SetColumnTitlesVisible(true);
        KfnResultsTree.HideRoot = true;

        // Create the root of the tree
        _kfnRoot = KfnResultsTree.CreateItem();

        // Connect the double-click event
        KfnResultsTree.ItemActivated += OnKfnItemDoubleClicked;
    }

    private void SetupKNTree()
    {
        KNResultsTree.Columns = 3;
        KNResultsTree.SetColumnTitle(0, "Song Name");
        KNResultsTree.SetColumnTitle(1, "Artist Name");
        KNResultsTree.SetColumnTitle(2, "Creator");
        KNResultsTree.SetColumnTitlesVisible(true);
        KNResultsTree.HideRoot = true;

        // Create the root of the tree
        _knRoot = KNResultsTree.CreateItem();

        // Connect the double-click event
        KNResultsTree.ItemActivated += OnKNItemDoubleClicked;
    }

    private void SetupLocalFilesTree()
    {
        LocalFilesResultsTree.Columns = 5;
        LocalFilesResultsTree.SetColumnTitle(0, "Song Name");
        LocalFilesResultsTree.SetColumnTitle(1, "Artist Name");

        LocalFilesResultsTree.SetColumnTitle(2, "Type");
        LocalFilesResultsTree.SetColumnExpand(2, false);
        LocalFilesResultsTree.SetColumnCustomMinimumWidth(2, 50);

        LocalFilesResultsTree.SetColumnTitle(3, "Creator");
        
        LocalFilesResultsTree.SetColumnTitle(4, "Identifier");
        LocalFilesResultsTree.SetColumnExpand(4, false);
        LocalFilesResultsTree.SetColumnCustomMinimumWidth(4, 150);

        LocalFilesResultsTree.SetColumnTitlesVisible(true);
        LocalFilesResultsTree.HideRoot = true;

        // Create the root of the tree
        _localFilesRoot = LocalFilesResultsTree.CreateItem();

        // Connect the double-click event
        LocalFilesResultsTree.ItemActivated += OnLocalFileItemDoubleClicked;
    }

    private void CloseAddToQueueDialog()
    {
        itemBeingAdded = null;
        AddToQueueDialog.Hide();
    }

    private void DisableOrEnableAddToQueueOkButton()
    {
        var okButton = AddToQueueDialog.GetOkButton();
        if (okButton != null)
        {
            okButton.Disabled = 
                isAddToQueueResolvingPerformLink
                || string.IsNullOrWhiteSpace(EnterSingerName.Text);
        }
    }

    private void AddToQueueDialogConfirmed()
    {
        if (itemBeingAdded != null 
        && !isAddToQueueResolvingPerformLink
        && !string.IsNullOrWhiteSpace(EnterSingerName.Text))
        {
            itemBeingAdded.SingerName = EnterSingerName.Text;
            EnterSingerName.Text = "";

            EmitSignal(SignalName.ItemAddedToQueue, itemBeingAdded);

            CloseAddToQueueDialog();
        }
    }

    private async Task ToggleIsSearching(bool isSearching)
    {
        SearchText.Editable = !isSearching;
        SearchButton.Disabled = isSearching;
        SearchButton.Text = isSearching ? "Searching..." : "Search";
        isStreamingKfnResults = isSearching;
        Input.SetDefaultCursorShape(isSearching ? Input.CursorShape.Busy : Input.CursorShape.Arrow);
        await ToSignal(GetTree(), "process_frame");
    }

    private async void Search(string query)
    {
        if (isStreamingKfnResults)
        {
            GD.Print("Already streaming results, skipping search.");
            return;
        }
        SearchCancellationSource.Cancel(); // Cancel any existing searches
        SearchCancellationSource = new CancellationTokenSource();
        await ToggleIsSearching(true);

        var searchKaraokenerds = SearchKaraokeNerdsCheckBox.ButtonPressed;
        var searchKarafun = SearchKarafunCheckBox.ButtonPressed;
        var searchLocalFiles = SearchLocalFilesCheckBox.ButtonPressed;

        KNResultsTree.Clear();
        KfnResultsTree.Clear();
        LocalFilesResultsTree.Clear();

        var searchTasks = new List<Task>();
        if (searchKaraokenerds)
        {

            _knRoot = KNResultsTree.CreateItem(); // Recreate the root item after clearing the tree
            searchTasks.Add(GetResultsFromKaraokenerds(query));
        }
        if (searchKarafun)
        {

            _kfnRoot = KfnResultsTree.CreateItem(); // Recreate the root item after clearing the tree
            searchTasks.Add(StreamResultsFromKarafun(query, SearchCancellationSource.Token));
        }
        if (searchLocalFiles)
        {
            _localFilesRoot = LocalFilesResultsTree.CreateItem(); // Recreate the root item after clearing the tree
            await SearchLocalFiles(query, SearchCancellationSource.Token);
        }
        await Task.WhenAll(searchTasks);
        await ToggleIsSearching(false);
    }

    private async Task GetResultsFromKaraokenerds(string query)
    {
        GD.Print($"Searching KN for: {query}");
        KNResultCount.SetLoaded(false);
        var results = await KaraokenerdsSearchScrape.Search(query);
        GD.Print($"Received {results.Count} results from KN");
        KNResults = results;
        foreach (var result in KNResults)
        {
            AddKNResultsRow(result);
        }
        KNResultCount.SetLoaded(true, $"{KNResults.Count}");
        await ToSignal(GetTree(), "process_frame");
    }

    private async Task SearchLocalFiles(string query, CancellationToken cancellationToken)
    {
        GD.Print($"Searching local files for: {query}");
        LocalFilesResultCount.SetLoaded(false);
        LocalFilesResults = await _localSearcher.Search(query, cancellationToken);
        GD.Print($"Found {LocalFilesResults.Count} local files");
        LocalFilesResultCount.SetLoaded(true, $"{LocalFilesResults.Count()}");
        await UpdateLocalFilesResultsTree();
    }
    

    private async Task StreamResultsFromKarafun(string query, CancellationToken cancellationToken)
    {
        GD.Print($"Searching Karafun for: {query}");
        KfnResultCount.SetLoaded(false);
        isStreamingKfnResults = true;
        KarafunApiResults = new List<KarafunApiSong>();
        
        var roomCode = Settings?.KarafunRoomCode;
        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
        {
            GD.Print("No valid Karafun room code available - Karafun search requires a connected room");
            KfnResultCount.SetLoaded(true, "0 (no room code)");
            isStreamingKfnResults = false;
            return;
        }
        
        await foreach (var result in KarafunApiSearch.Search(roomCode, query, cancellationToken))
        {
            if (result?.Songs == null) continue;
            
            GD.Print($"Received {result.Songs.Count} results from Karafun API");
            KarafunApiResults.AddRange(result.Songs);
            
            // Deduplicate by song ID
            KarafunApiResults = KarafunApiResults
                .DistinctBy(s => s.SongId)
                .ToList();

            await UpdateKarafunResultsTree();

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        isStreamingKfnResults = false;
        KfnResultCount.SetLoaded(true, $"{KarafunApiResults.Count}");
    }

    private async Task UpdateKarafunResultsTree()
    {
        // Track user selections
        var selectedItems = new List<string>();
        var selectedItem = KfnResultsTree.GetSelected();
        if (selectedItem != null)
        {
            selectedItems.Add(selectedItem.GetMetadata(0).ToString()); // Use metadata to track selections
        }

        //actually probably fine to just clear and re-add everything
        KfnResultsTree.Clear();
        _kfnRoot = KfnResultsTree.CreateItem(); // Recreate the root item after clearing the tree
        foreach (var result in KarafunApiResults)
        {
            AddKarafunResultsRow(result);
        }

        // Restore user selections
        foreach (var item in selectedItems)
        {
            var treeItem = FindTreeItemByMetadata(item);
            if (treeItem != null)
            {
                KfnResultsTree.SetSelected(treeItem, 0);
            }
        }

        await ToSignal(GetTree(), "process_frame");
    }

    private async Task UpdateLocalFilesResultsTree() // TODO: review
    {
        // Track user selections
        var selectedItems = new List<string>();
        var selectedItem = LocalFilesResultsTree.GetSelected();
        if (selectedItem != null)
        {
            selectedItems.Add(selectedItem.GetMetadata(0).ToString()); // Use metadata to track selections
        }

        //actually probably fine to just clear and re-add everything
        LocalFilesResultsTree.Clear();
        _kfnRoot = LocalFilesResultsTree.CreateItem(); // Recreate the root item after clearing the tree
        foreach (var result in LocalFilesResults)
        {
            AddLocalFilesResultsRow(result);
        }

        // Restore user selections
        foreach (var item in selectedItems)
        {
            var treeItem = FindTreeItemByMetadata(item);
            if (treeItem != null)
            {
                LocalFilesResultsTree.SetSelected(treeItem, 0);
            }
        }

        await ToSignal(GetTree(), "process_frame");
    }

    private TreeItem FindTreeItemByMetadata(string metadata)
    {
        var items = _kfnRoot.GetChildren();
        var item = items.FirstOrDefault();
        while (item != null)
        {
            if (item.GetMetadata(0).ToString() == metadata)
            {
                return item;
            }
            item = item.GetNext();
        }
        return null;
    }

    private void AddKarafunResultsRow(KarafunApiSong song)
    {
        if (_kfnRoot == null)
        {
            GD.Print("Kfn root item is disposed, recreating it.");
            _kfnRoot = KfnResultsTree.CreateItem();
        }
        var treeItem = KfnResultsTree.CreateItem(_kfnRoot);
        treeItem.SetText(0, song.Title);
        treeItem.SetText(1, song.Artist);
        // Store song ID as metadata for use when adding to queue
        treeItem.SetMetadata(0, song.SongId.ToString());
    }

    private void AddKNResultsRow(KNSearchResultItem item)
    {
        if (_knRoot == null)
        {
            GD.Print("KN root item is disposed, recreating it.");
            _knRoot = KNResultsTree.CreateItem();
        }
        var treeItem = KNResultsTree.CreateItem(_knRoot);
        treeItem.SetText(0, item.SongName);
        treeItem.SetText(1, item.ArtistName);
        treeItem.SetText(2, item.CreatorBrandName);
        treeItem.SetMetadata(0, item.YoutubeLink);
    }

    private void AddLocalFilesResultsRow(LocalSongFileEntry entry)
    {
        if (_localFilesRoot == null)
        {
            GD.Print("Local files root item is disposed, recreating it.");
            _localFilesRoot = LocalFilesResultsTree.CreateItem();
        }
        var treeItem = LocalFilesResultsTree.CreateItem(_kfnRoot);
        treeItem.SetText(0, entry.SongName);
        treeItem.SetText(1, entry.ArtistName);
        treeItem.SetText(2, Path.GetExtension(entry.FullPath));
        treeItem.SetText(3, entry.CreatorName);
        treeItem.SetText(4, entry.Identifier);
        treeItem.SetMetadata(0, entry.FullPath);
    }

    private void ShowAddToQueueDialog(string creatorName, bool isLoaded, string songName = null, string artistName = null)
    {
        SetAddToQueueBoxText(creatorName, isLoaded, songName, artistName);
        AddToQueueDialog.PopupCentered();
        DisableOrEnableAddToQueueOkButton();
        EnterSingerName.GrabFocus();
    }

    private void SetAddToQueueBoxText(string creatorName, bool isLoaded, string songName = null, string artistName = null)
    {
        QueueAddCreatorNameLabel.Text = creatorName;
        QueueAddSongNameLabel.SetLoaded(isLoaded, songName);
        QueueAddArtistNameLabel.SetLoaded(isLoaded, artistName);
    }


    private void SetResolvingPerformLink(bool isResolving)
    {
        isAddToQueueResolvingPerformLink = isResolving;
        DisableOrEnableAddToQueueOkButton();
    }

    private void OnKfnItemDoubleClicked()
    {
        TreeItem selectedItem = KfnResultsTree.GetSelected();
        if (selectedItem != null)
        {
            string songName = selectedItem.GetText(0);
            string artistName = selectedItem.GetText(1);
            string songId = selectedItem.GetMetadata(0).ToString();
            
            GD.Print($"Double-clicked Karafun: {songName} by {artistName} (ID: {songId})");

            // With the API, we have the song ID directly - no need to resolve a perform link
            itemBeingAdded = new QueueItem
            {
                SongName = songName,
                ArtistName = artistName,
                CreatorName = "Karafun",
                Identifier = songId,
                // Generate a performance link for fallback/browser mode if needed
                PerformanceLink = $"https://www.karafun.com/karaoke/{songId}/",
                ItemType = ItemType.KarafunRemote
            };

            ShowAddToQueueDialog("Karafun", true, songName, artistName);
        }
    }

    private string CleanYoutubeLink(string url)
    {
        // Check if the URL is valid (it must be `?v=`)
		if (string.IsNullOrWhiteSpace(url) || !url.Contains("?v="))
		{
			GD.PrintErr($"Not a youtube '?v=' URL: {url}");
			return url; // TODO: this would not be helpful. We should just return null or throw
		}        

		// and we should cut off any additional query params because they can be playlists and other disruptive things
		int ampIndex = url.IndexOf("&");
		if (ampIndex != -1)
		{
			GD.PushWarning($"Removed additional queryparams from youtube URL '{url}'");
			url = url.Substring(0, ampIndex);
		}
		return url;
    }

    private void OnKNItemDoubleClicked()
    {
        TreeItem selectedItem = KNResultsTree.GetSelected();
        if (selectedItem != null)
        {
            string songName = selectedItem.GetText(0);
            string artistName = selectedItem.GetText(1);
            string creatorName = selectedItem.GetText(2);
            string youtubeLink = CleanYoutubeLink(selectedItem.GetMetadata(0).ToString());
            GD.Print($"Double-clicked: {songName} by {artistName} ({creatorName}), {youtubeLink}");
            itemBeingAdded = new QueueItem
            {
                SongName = songName,
                ArtistName = artistName,
                CreatorName = creatorName,
                PerformanceLink = youtubeLink,
                ItemType = ItemType.Youtube
            };

            SetResolvingPerformLink(false);
            ShowAddToQueueDialog(creatorName, true, songName, artistName);
        }
    }

    private void OnLocalFileItemDoubleClicked() // TODO: this doesn't belong here
    {
        TreeItem selectedItem = LocalFilesResultsTree.GetSelected();
        if (selectedItem != null)
        {
            var localFilePath = selectedItem.GetMetadata(0).ToString();
            string songName = selectedItem.GetText(0);
            string artistName = selectedItem.GetText(1);
            string fileType = selectedItem.GetText(2);
            string creatorName = selectedItem.GetText(3);
            GD.Print($"Double-clicked: {songName} by {artistName} ({creatorName}), {localFilePath}");
            itemBeingAdded = new QueueItem
            {
                SongName = songName,
                ArtistName = artistName,
                CreatorName = creatorName,
                PerformanceLink = localFilePath,
                ItemType = fileType switch
                {
                    ".zip" => ItemType.LocalMp3GZip,
                    ".cdg" => ItemType.LocalMp3G,
                    ".mp3" => ItemType.LocalMp3G,
                    ".mp4" => ItemType.LocalMp4,
                    _ => throw new NotImplementedException()
                }
            };

            SetResolvingPerformLink(false);
            ShowAddToQueueDialog(creatorName ?? "(local file)", true, songName, artistName);
        }
    }
}
