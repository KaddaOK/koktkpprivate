using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface ISearchTab : IMarginContainer
{
    void ExternalFileShowAddDialog(QueueItem item);
    event SearchTab.ItemAddedToQueueEventHandler ItemAddedToQueue;
}

[Meta(typeof(IAutoNode))]
public partial class SearchTab : MarginContainer, ISearchTab
{
    public override void _Notification(int what) => this.Notify(what);

    private List<KarafunSearchScrapeResultItem> KarafunResults;
    private List<KNSearchResultItem> KNResults;
    private bool isStreamingResults = false;

    private QueueItem itemBeingAdded;

    private bool isAddToQueueResolvingPerformLink = false;

    private TreeItem _kfnRoot;
    private TreeItem _knRoot;

    #region Nodes

    [Node] private ConfirmationDialog AddToQueueDialog { get; set; } = default!;
    [Node] private LineEdit EnterSingerName { get; set; } = default!;
    [Node] private Tree KfnResultsTree { get; set; } = default!;
    [Node] private ILoadableLabel KfnResultCount { get; set; } = default!;
    [Node] private Tree KNResultsTree { get; set; } = default!;
    [Node] private ILoadableLabel KNResultCount { get; set; } = default!;
    [Node] private LineEdit SearchText { get; set; } = default!;
    [Node] private Button SearchButton { get; set; } = default!;
    [Node] private Button ClearSearchButton { get; set; } = default!;
    [Node] private ILoadableLabel QueueAddSongNameLabel { get; set; } = default!;
    [Node] private ILoadableLabel QueueAddArtistNameLabel { get; set; } = default!;
    [Node] private Label QueueAddCreatorNameLabel { get; set; } = default!;

    #endregion

    #region Signals

    [Signal]
    public delegate void ItemAddedToQueueEventHandler(QueueItem itemBeingAdded);

    #endregion

    public void OnReady()
    {
        SetupKfnTree();
        SetupKNTree();

        SearchText.TextSubmitted += Search;
        SearchButton.Pressed += () => Search(SearchText.Text);
        ClearSearchButton.Pressed += ClearSearch;

        EnterSingerName.TextChanged += (_) => DisableOrEnableAddToQueueOkButton();
        EnterSingerName.TextSubmitted += (_) => AddToQueueDialogConfirmed();
        AddToQueueDialog.Confirmed += AddToQueueDialogConfirmed;
        AddToQueueDialog.Canceled += CloseAddToQueueDialog;

        KfnResultCount.SetLoaded(true, "");
        KNResultCount.SetLoaded(true, "");
    }


    private void ClearSearch()
    {
        SearchText.Text = "";
        KfnResultsTree.Clear();
        KNResultsTree.Clear();
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
        isStreamingResults = isSearching;
        Input.SetDefaultCursorShape(isSearching ? Input.CursorShape.Busy : Input.CursorShape.Arrow);
        await ToSignal(GetTree(), "process_frame");
    }

    private async void Search(string query)
    {
        if (isStreamingResults)
        {
            GD.Print("Already streaming results, skipping search.");
            return;
        }
        await ToggleIsSearching(true);

        var searchKaraokenerds = true; // TODO: Implement a setting to enable/disable searching Karaokenerds?
        var searchKarafun = true; // TODO: Implement a setting to enable/disable searching Karafun?

        var searchTasks = new List<Task>();
        if (searchKaraokenerds)
        {
            KNResultsTree.Clear();
            _knRoot = KNResultsTree.CreateItem(); // Recreate the root item after clearing the tree
            searchTasks.Add(GetResultsFromKaraokenerds(query));
        }
        if (searchKarafun)
        {
            KfnResultsTree.Clear();
            _kfnRoot = KfnResultsTree.CreateItem(); // Recreate the root item after clearing the tree
            searchTasks.Add(StreamResultsFromKarafun(query));
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

    private async Task StreamResultsFromKarafun(string query)
    {
        GD.Print($"Searching Karafun for: {query}");
        KfnResultCount.SetLoaded(false);
        isStreamingResults = true;
        var mayHaveMore = false;
        var pageResults = new List<KarafunSearchScrapeResultItem>();
        var artistResults = new Dictionary<string, List<KarafunSearchScrapeResultItem>>();
        KarafunResults = new List<KarafunSearchScrapeResultItem>();
        await foreach (var result in KarafunSearchScrape.Search(query))
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
        }
        isStreamingResults = false;
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

    private void OnKNItemDoubleClicked()
    {
        TreeItem selectedItem = KNResultsTree.GetSelected();
        if (selectedItem != null)
        {
            string songName = selectedItem.GetText(0);
            string artistName = selectedItem.GetText(1);
            string creatorName = selectedItem.GetText(2);
            string youtubeLink = selectedItem.GetMetadata(0).ToString();
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
}
