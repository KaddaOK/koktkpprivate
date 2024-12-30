using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class SearchTab : MarginContainer 
{
	public override void _Ready()
	{
		BindSearchScreenControls();
	}

	private Tree KfnResultsTree;
	private Tree KNResultsTree;
	private TreeItem _kfnRoot;
	private TreeItem _knRoot;
	private LineEdit SearchText;
	private Button SearchButton;
	private Button ClearSearchButton;
	private List<KarafunSearchScrapeResultItem> KarafunResults;
	private List<KNSearchResultItem> KNResults;
	private Boolean IsStreamingResults = false;

	public QueueItem ItemBeingAdded { get; private set; }

	private ConfirmationDialog AddToQueueDialog;
	private LineEdit EnterSingerName;
	private bool IsAddToQueueResolvingPerformLink = false;
	private Label QueueAddSongNameLabel;
	private Label QueueAddArtistNameLabel;
	private Label QueueAddCreatorNameLabel;

	private void BindSearchScreenControls()
	{
		SetupKfnTree();
		SetupKNTree();
		SetupSearchText();
		SetupSearchButton();
		SetupAddToQueueDialog();
	}

	private void SetupSearchText()
	{
		SearchText = GetNode<LineEdit>($"%{nameof(SearchText)}");
		SearchText.TextSubmitted += Search;
	}
	
	private void SetupSearchButton()
	{
		SearchButton = GetNode<Button>($"%{nameof(SearchButton)}");
		SearchButton.Pressed += () => Search(SearchText.Text);

		ClearSearchButton = GetNode<Button>($"%{nameof(ClearSearchButton)}");
		ClearSearchButton.Pressed += () => {
			SearchText.Text = "";
			KfnResultsTree.Clear();
			KNResultsTree.Clear();
			SearchText.GrabFocus();
		};
	}

	private void SetupKfnTree()
	{
		KfnResultsTree = GetNode<Tree>($"%{nameof(KfnResultsTree)}");
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
		KNResultsTree = GetNode<Tree>($"%{nameof(KNResultsTree)}");
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

	private void SetupAddToQueueDialog()
	{
		AddToQueueDialog = GetNode<ConfirmationDialog>($"%{nameof(AddToQueueDialog)}");
		EnterSingerName = AddToQueueDialog.GetNode<LineEdit>($"%{nameof(EnterSingerName)}");
		EnterSingerName.TextSubmitted += (_) => AddToQueueDialogConfirmed();
		QueueAddSongNameLabel = AddToQueueDialog.GetNode<Label>($"%{nameof(QueueAddSongNameLabel)}");
		QueueAddArtistNameLabel = AddToQueueDialog.GetNode<Label>($"%{nameof(QueueAddArtistNameLabel)}");
		QueueAddCreatorNameLabel = AddToQueueDialog.GetNode<Label>($"%{nameof(QueueAddCreatorNameLabel)}");
		AddToQueueDialog.Confirmed += AddToQueueDialogConfirmed;
		AddToQueueDialog.Canceled += CloseAddToQueueDialog;
	}

	private void CloseAddToQueueDialog()
	{
		ItemBeingAdded = null;
		AddToQueueDialog.Hide();
	}

	#region Signals
	[Signal]
	public delegate void ItemAddedToQueueEventHandler(QueueItem itemBeingAdded);
	#endregion

	private void AddToQueueDialogConfirmed()
	{
		if (ItemBeingAdded != null && !IsAddToQueueResolvingPerformLink)
		{
			ItemBeingAdded.SingerName = EnterSingerName.Text;
			EnterSingerName.Text = "";

			EmitSignal(SignalName.ItemAddedToQueue, ItemBeingAdded);

			CloseAddToQueueDialog();
		}
	}

	private async Task ToggleIsSearching(bool isSearching)
	{
		SearchText.Editable = !isSearching;
		SearchButton.Disabled = isSearching;
		SearchButton.Text = isSearching ? "Searching..." : "Search";
		IsStreamingResults = isSearching;
		Input.SetDefaultCursorShape(isSearching ? Input.CursorShape.Busy : Input.CursorShape.Arrow);
		await ToSignal(GetTree(), "process_frame");
	}

	private async void Search(string query)
	{
		if (IsStreamingResults)
		{
			GD.Print("Already streaming results, skipping search.");
			return;
		}
		await ToggleIsSearching(true);

		var searchKaraokenerds = true; // TODO: Implement a setting to enable/disable searching Karaokenerds
		var searchKarafun = true; // TODO: Implement a setting to enable/disable searching Karafun

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
		var results = await KaraokenerdsSearchScrape.Search(query);
		GD.Print($"Received {results.Count} results from KN");
		KNResults = results;
		foreach (var result in KNResults)
		{
			AddKNResultsRow(result);
		}
		await ToSignal(GetTree(), "process_frame");
	}

	private async Task StreamResultsFromKarafun(string query)
	{
		GD.Print($"Searching Karafun for: {query}");
		IsStreamingResults = true;
		var mayHaveMore = false;
		var pageResults = new List<KarafunSearchScrapeResultItem>();
		var artistResults = new Dictionary<string, List<KarafunSearchScrapeResultItem>>();
		KarafunResults = new List<KarafunSearchScrapeResultItem>();
		await foreach (var result in KarafunSearchScrape.Search(query))
		{
			GD.Print($"Received {result.Results.Count} results from Karafun");
			if (result.MayHaveMore) {
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
		IsStreamingResults = false;
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

	private void ShowAddToQueueDialog(string songName, string artistName, string creatorName)
	{
		SetAddToQueueBoxText(songName, artistName, creatorName);
		AddToQueueDialog.PopupCentered();
		EnterSingerName.GrabFocus();
	}
	private void SetAddToQueueBoxText(string songName, string artistName, string creatorName)
	{
		QueueAddSongNameLabel.Text = songName;
		QueueAddArtistNameLabel.Text = artistName;
		QueueAddCreatorNameLabel.Text = creatorName;
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

			IsAddToQueueResolvingPerformLink = true;
			ShowAddToQueueDialog("Loading, please wait...", "Loading, please wait...", "Karafun (loading perform link)");
			await ToSignal(GetTree(), "process_frame");

			GD.Print($"Getting perform link for {songInfoLink}");
			var performLink = await KarafunSearchScrape.GetDirectPerformanceLinkForSong(songInfoLink);
			GD.Print($"Perform link: {performLink}");

			ItemBeingAdded = new QueueItem
			{
				SongName = songName,
				ArtistName = artistName,
				CreatorName = "Karafun",
				SongInfoLink = songInfoLink,
				PerformanceLink = performLink,
				ItemType = ItemType.KarafunWeb
			};

			IsAddToQueueResolvingPerformLink = false;
			SetAddToQueueBoxText(songName, artistName, "Karafun Web");
		}
	}

	private async void OnKNItemDoubleClicked()
	{
		TreeItem selectedItem = KNResultsTree.GetSelected();
		if (selectedItem != null)
		{
			string songName = selectedItem.GetText(0);
			string artistName = selectedItem.GetText(1);
			string creatorName = selectedItem.GetText(2);
			string youtubeLink = selectedItem.GetMetadata(0).ToString();
			GD.Print($"Double-clicked: {songName} by {artistName} ({creatorName}), {youtubeLink}");
			
			ItemBeingAdded = new QueueItem
			{
				SongName = songName,
				ArtistName = artistName,
				CreatorName = creatorName,
				PerformanceLink = youtubeLink,
				ItemType = ItemType.Youtube
			};

			IsAddToQueueResolvingPerformLink = false;
			ShowAddToQueueDialog(songName, artistName, creatorName);
		}
	}
}
