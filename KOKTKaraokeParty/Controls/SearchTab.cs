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

    private List<KarafunSearchScrapeResultItem> KarafunResults;
    private List<KNSearchResultItem> KNResults;
    private List<LocalSongFileEntry> LocalFilesResults;
    private bool isStreamingKfnResults = false;

    private QueueItem itemBeingAdded;

    private bool isAddToQueueResolvingPerformLink = false;

    private TreeItem _kfnRoot;
    private TreeItem _knRoot;
    private TreeItem _localFilesRoot;

    private CancellationTokenSource SearchCancellationSource = new CancellationTokenSource();

    /*
    private ILocalFileScanner _localFileScanner = new LocalFileScanner(); // TODO: this doesn't belong here
    private ILocalFileValidator _localFileValidator = new LocalFileValidator(); // TODO: this doesn't belong here
    private List<string> LocalFilesResults = new List<string>(); // TODO: this doesn't belong here
    */
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

/*
    private async void StreamResultsFromLocalFiles(string query, CancellationToken cancellationToken)
    {
        GD.Print($"Searching local files for: {query}");
        LocalFilesResultCount.SetLoaded(false);
        LocalFilesResults = new List<string>();
        var searchTerms = query.Split(' ');
        int i = 0;
        await foreach (var result in _localFileScanner.FindAllFilesAsync(@"\\SCORPIO\karaoke2" // TODO: fix this hardcoded path!
        , cancellationToken))
        {
            if (searchTerms.All(term => result.IndexOf(term, StringComparison.OrdinalIgnoreCase) != -1)
                // && _localFileValidator.IsValid(result).isValid //TODO: can't do this for performance reasons
                )
            {
                LocalFilesResults.Add(result);
                i++;
                if (i % 5 == 0)
                {
                    //await UpdateLocalFilesResultsTree();
                }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        LocalFilesResultCount.SetLoaded(true, $"{LocalFilesResults.Count()}");
        await UpdateLocalFilesResultsTree();
    }*/
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
        var mayHaveMore = false;
        var pageResults = new List<KarafunSearchScrapeResultItem>();
        var artistResults = new Dictionary<string, List<KarafunSearchScrapeResultItem>>();
        KarafunResults = new List<KarafunSearchScrapeResultItem>();
        await foreach (var result in KarafunSearchScrape.Search(query, cancellationToken))
        {
            GD.Print($"Received {result.Results.Count} results from Karafun");
            if (result.MayHaveMore)
            {
                mayHaveMore = true;
            }
            if (result.PartOfArtistSet != null)
            {
                if (artistResults.ContainsKey(result.PartOfArtistSet))
                {
                    artistResults[result.PartOfArtistSet].AddRange(result.Results);
                }
                else
                {
                    artistResults.Add(result.PartOfArtistSet, result.Results);
                }
            }
            else
            {
                pageResults.AddRange(result.Results);
            }

            KarafunResults = new List<KarafunSearchScrapeResultItem>(pageResults);
            foreach (var artist in artistResults)
            {
                // Find the index of the Artist item and replace it with the new results
                int artistIndex = KarafunResults.FindIndex(a => a.ResultType == KarafunSearchScrapeResultItemType.Artist && a.ArtistLink == artist.Key);
                if (artistIndex != -1)
                {
                    KarafunResults.RemoveAt(artistIndex);
                    KarafunResults.InsertRange(artistIndex, artist.Value);
                }
            }
            KarafunResults = KarafunResults
                .Where(r => r.ResultType != KarafunSearchScrapeResultItemType.Artist)
                .DistinctBy(r => r.SongInfoLink)
                .OrderBy(item => item.ResultType == KarafunSearchScrapeResultItemType.UnlicensedSong ? 1 : 0)
                .ThenBy(item => KarafunResults.IndexOf(item)) // Preserve the original relative order
                .ToList();

            await UpdateKarafunResultsTree();

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        isStreamingKfnResults = false;
        KfnResultCount.SetLoaded(true, $"{KarafunResults.Count}");//{(mayHaveMore ? "*" : "")}");
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

        //GD.Print($"Updating karafun tree with {KarafunResults.Count} results");

        //actually probably fine to just clear and re-add everything
        KfnResultsTree.Clear();
        _kfnRoot = KfnResultsTree.CreateItem(); // Recreate the root item after clearing the tree
        foreach (var result in KarafunResults)
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

    private void AddKarafunResultsRow(KarafunSearchScrapeResultItem item)
    {
        if (_kfnRoot == null)
        {
            GD.Print("Kfn root item is disposed, recreating it.");
            _kfnRoot = KfnResultsTree.CreateItem();
        }
        var treeItem = KfnResultsTree.CreateItem(_kfnRoot);
        treeItem.SetText(0, item.SongName);
        treeItem.SetText(1, item.ArtistName);
        treeItem.SetMetadata(0, item.SongInfoLink);
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

    /*private void AddLocalFilesResultsRow(string path) // TODO: this doesn't belong here 
    {
        if (_localFilesRoot == null)
        {
            GD.Print("Local files root item is disposed, recreating it.");
            _localFilesRoot = LocalFilesResultsTree.CreateItem();
        }
        var treeItem = LocalFilesResultsTree.CreateItem(_kfnRoot);
        treeItem.SetText(0, Path.GetFileNameWithoutExtension(path));
        treeItem.SetMetadata(0, path);
    }*/

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

    private async void OnKfnItemDoubleClicked()
    {
        TreeItem selectedItem = KfnResultsTree.GetSelected();
        if (selectedItem != null)
        {
            string songName = selectedItem.GetText(0);
            string artistName = selectedItem.GetText(1);
            GD.Print($"Double-clicked: {songName} by {artistName}");
            string songInfoLink = selectedItem.GetMetadata(0).ToString();

            SetResolvingPerformLink(true);
            Input.SetDefaultCursorShape(Input.CursorShape.Busy);
            ShowAddToQueueDialog("Karafun (loading perform link...)", false);
            await ToSignal(GetTree(), "process_frame");

            GD.Print($"Getting perform link for {songInfoLink}");
            var performLink = await KarafunSearchScrape.GetDirectPerformanceLinkForSong(songInfoLink);
            GD.Print($"Perform link: {performLink}");

            itemBeingAdded = new QueueItem
            {
                SongName = songName,
                ArtistName = artistName,
                CreatorName = "Karafun",
                SongInfoLink = songInfoLink,
                PerformanceLink = performLink,
                ItemType = ItemType.KarafunWeb
            };

            SetResolvingPerformLink(false);
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            DisableOrEnableAddToQueueOkButton();
            SetAddToQueueBoxText("Karafun Web", true, songName, artistName);
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
