using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public interface IEditScanPathDialog : IConfirmationDialog
{
    void SetScanPathEntry(LocalScanPathEntry scanPathEntry);
    event EditScanPathDialog.ScanPathSavedEventHandler ScanPathSaved;
}

[Meta(typeof(IAutoNode))]
public partial class EditScanPathDialog : ConfirmationDialog, IEditScanPathDialog
{
    public override void _Notification(int what) => this.Notify(what);

    private LocalScanPathEntry _scanPathEntryToBeEdited;

    private bool _isCustomFormat;
    private List<string> _matchingFiles = new List<string>();
    private TreeItem _exampleResultsRoot;

    #region Nodes
    [Node] private ILabel SelectedPathLabel { get; set; } = default!;
    [Node] private IButton BrowsePathButton { get; set; } = default!;
    [Node] private IOptionButton FormatOptionButton { get; set; } = default!;
    [Node] private IFileDialog EditPathFileDialog { get; set; } = default!;
    [Node] private IHBoxContainer CustomFormatRow { get; set; } = default!;
    [Node] private ILineEdit CustomFormatLineEdit { get; set; } = default!;
    [Node] private ILabel ValidationErrorLabel { get; set; } = default!;
    [Node] private IHBoxContainer MatchingFilesLoadingHBox { get; set; } = default!;
    [Node] private IButton MatchingFilesRefreshButton { get; set; } = default!;
    [Node] private IItemList MatchingFilesItemList { get; set; } = default!;
    [Node] private IVBoxContainer ExampleResultsPane { get; set; } = default!;
    [Node] private ITree ExampleResultsTree { get; set; } = default!;
    #endregion

    #region Signals
    [Signal]
    public delegate void ScanPathSavedEventHandler(int scanPathEntryId);
    #endregion

    #region Initialized Dependencies

    private ILocalFileNameMetadataParser MetadataParser { get; set; }
    private KOKTDbContext DbContext { get; set; }
    private ILocalFileScanner LocalFileScanner { get; set; }

    public void SetupForTesting(ILocalFileNameMetadataParser parser, ILocalFileScanner scanner, KOKTDbContext dbContext)
    {
        MetadataParser = parser;
        LocalFileScanner = scanner;
        DbContext = dbContext;
    }

    public void Initialize()
    {
        MetadataParser = new LocalFileNameMetadataParser();
        LocalFileScanner = new LocalFileScanner();
        DbContext = new KOKTDbContext();
        DbContext.Database.EnsureCreated();
    }

    #endregion

    public void OnReady()
    {
        BrowsePathButton.Pressed += BrowsePathButtonPressed;
        FormatOptionButton.ItemSelected += FormatOptionButtonItemSelected;
        EditPathFileDialog.DirSelected += EditPathFileDialogDirSelected;
        Confirmed += SaveButtonPressed;
        CustomFormatLineEdit.TextChanged += (string newText) =>
        {
            DisableOrEnableSaveButton();
        };
        MatchingFilesRefreshButton.Pressed += RefreshMatchingFiles;

        ExampleResultsTree.Columns = 4;
        ExampleResultsTree.SetColumnTitle(0, "Artist");
        ExampleResultsTree.SetColumnTitle(1, "Title");
        ExampleResultsTree.SetColumnTitle(2, "Identifier");
        ExampleResultsTree.SetColumnTitle(3, "Creator");
        ExampleResultsTree.SetColumnTitlesVisible(true);
        ExampleResultsTree.HideRoot = true;

        // Create the root of the tree
        _exampleResultsRoot = ExampleResultsTree.CreateItem();

        DisableOrEnableSaveButton();
    }

    private void DisableOrEnableSaveButton()
    {
        var okButton = GetOkButton();
        if (okButton != null)
        {
            var (isValid, errorMessage) = GetValidationErrorMessage();
            ShowResults(isValid && _matchingFiles?.Any() == true);

            // if we have matching files, always show a validation error (they may not have entered a format yet but that's ok)
            if (_matchingFiles?.Any() == true)
            {
                ValidationErrorLabel.Visible = !isValid;
                ValidationErrorLabel.Text = errorMessage;
            }

            // either way the button should be disabled if they haven't even entered enough stuff yet
            // (I think this makes sense but it might not; TODO revisit when head clearer)
            okButton.Disabled = 
                string.IsNullOrWhiteSpace(SelectedPathLabel.Text) || 
                (!_isCustomFormat && FormatOptionButton.Selected == -1) ||
                (_isCustomFormat && string.IsNullOrWhiteSpace(CustomFormatLineEdit.Text));
        }
    }

    private string GetSelectedFormat()
    {
        return _isCustomFormat 
                ? CustomFormatLineEdit.Text 
                : FormatOptionButton.Selected == -1
                    ? null
                    : FormatOptionButton.GetItemText(FormatOptionButton.Selected);
    }

    private void ShowResults(bool show)
    {
        ExampleResultsPane.Visible = show;
        if (show)
        {
            ExampleResultsTree.Clear();
            if (_matchingFiles?.Any() == true)
            {
                var format = GetSelectedFormat();
                if (format != null)
                {
                    var parseResults = _matchingFiles.Select(f => MetadataParser.Parse(f, format, SelectedPathLabel.Text)).ToList();
                    foreach (var result in parseResults)
                    {
                        AddExampleResultsRow(result);
                    }
                }
            }
        }
    }

    private void AddExampleResultsRow(SongMetadata item)
    {
        if (item == null)
        {
            return;
        };
        var root = ExampleResultsTree.GetRoot();
        if (root == null)
        {
            GD.Print("Example results root item is disposed, recreating it.");
            root = ExampleResultsTree.CreateItem();
        }
        var treeItem = ExampleResultsTree.CreateItem(root);
        treeItem.SetText(0, item.ArtistName);
        treeItem.SetText(1, item.SongTitle);
        treeItem.SetText(2, item.Identifier);
        treeItem.SetText(3, item.CreatorName);
    }

    private (bool, string) GetValidationErrorMessage()
    {
        if (string.IsNullOrWhiteSpace(SelectedPathLabel.Text))
        {
            return (false, "Please select a path.");
        }

        if (_isCustomFormat)
        {
            if (_isCustomFormat && string.IsNullOrWhiteSpace(CustomFormatLineEdit.Text))
            {
                return (false, "Please enter a custom format.");
            }

            var isValid = MetadataParser.ValidateFormatSpecification(CustomFormatLineEdit.Text);
            if (!isValid.isValid)
            {
                return (false, isValid.validationError);
            }
        }

        return (true, "");
    }

    private void SaveButtonPressed()
    {
        var (isValid, errorMessage) = GetValidationErrorMessage();
        if (!isValid)
        {
            ValidationErrorLabel.Text = errorMessage;
            ValidationErrorLabel.Visible = true;
            return;
        }

        UpdateAndSaveEntry();
        SetScanPathEntry(new LocalScanPathEntry());
        Hide();
    }

    private void UpdateAndSaveEntry()
    {
        if (_scanPathEntryToBeEdited == null)
        {
            _scanPathEntryToBeEdited = new LocalScanPathEntry();
        }
        if (_scanPathEntryToBeEdited.Id < 1)
        {
            DbContext.LocalScanPaths.Add(_scanPathEntryToBeEdited);
        }

        _scanPathEntryToBeEdited.Path = SelectedPathLabel.Text;
        _scanPathEntryToBeEdited.FormatSpecifier = GetSelectedFormat();
        DbContext.SaveChanges();
        EmitSignal(SignalName.ScanPathSaved, _scanPathEntryToBeEdited.Id);
    }


    private void EditPathFileDialogDirSelected(string dir)
    {
        SelectedPathLabel.Text = dir;
        RefreshMatchingFiles();
        DisableOrEnableSaveButton();
    }

    private void FormatOptionButtonItemSelected(long index)
    {
        var formatOptionText = FormatOptionButton.GetItemText((int)index);
        var isCustom = formatOptionText == "Custom...";
        ToggleIsCustomFormat(isCustom);
        if (!isCustom)
        {
            CustomFormatLineEdit.Text = formatOptionText;
        }
        DisableOrEnableSaveButton();
    }


    private void BrowsePathButtonPressed()
    {
        EditPathFileDialog.Show();
    }

    public async void RefreshMatchingFiles()
    {
        if (string.IsNullOrWhiteSpace(SelectedPathLabel.Text))
        {
            return;
        }

        MatchingFilesItemList.Clear();
        _matchingFiles.Clear();

        MatchingFilesLoadingHBox.Visible = true;
        MatchingFilesRefreshButton.Visible = false;

        _matchingFiles = await LocalFileScanner.FindFirstFewFilesAsync(SelectedPathLabel.Text, 5, true, true, true, new System.Threading.CancellationToken());
        
        MatchingFilesLoadingHBox.Visible = false;

        foreach (var file in _matchingFiles)
        {
            MatchingFilesItemList.AddItem(file.Replace(SelectedPathLabel.Text, ""), selectable: false);
        }

        if (!_matchingFiles.Any())
        {
            MatchingFilesItemList.AddItem("No matching files found.", selectable: false);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(GetSelectedFormat()))
            {
                ShowResults(true);
            }
        }

        MatchingFilesItemList.Visible = true;
    }

    public void SetScanPathEntry(LocalScanPathEntry scanPathEntry)
    {
        _scanPathEntryToBeEdited = scanPathEntry;
        SelectedPathLabel.Text = _scanPathEntryToBeEdited?.Path;
        CustomFormatLineEdit.Text = _scanPathEntryToBeEdited?.FormatSpecifier;
        EditPathFileDialog.CurrentDir = _scanPathEntryToBeEdited?.Path;
        
        // if this is an existing entry, we don't have any examples until refresh is clicked, 
        // or if it's a new entry, there's nothing to refresh yet
        _matchingFiles.Clear();
        MatchingFilesLoadingHBox.Visible = false;
        MatchingFilesItemList.Visible = false;
        MatchingFilesRefreshButton.Visible = !string.IsNullOrWhiteSpace(SelectedPathLabel.Text);

        // shouldn't show validation errors on first load
        ValidationErrorLabel.Visible = false;

        // Clear example results also
        ExampleResultsTree.Clear();
        ShowResults(false);

        if (string.IsNullOrWhiteSpace(_scanPathEntryToBeEdited?.FormatSpecifier))
        {
            FormatOptionButton.Selected = -1; // Deselect any selected item
        }
        else
        {
            bool found = false;
            int custom = -1;
            for (int i = 0; i < FormatOptionButton.GetItemCount(); i++)
            {
                if (FormatOptionButton.GetItemText(i) == _scanPathEntryToBeEdited.FormatSpecifier)
                {
                    FormatOptionButton.Selected = i;
                    found = true;
                    break;
                }
                else if (FormatOptionButton.GetItemText(i) == "Custom...")
                {
                    custom = i;
                }
            }

            if (!found && custom != -1)
            {
                FormatOptionButton.Selected = custom;
                ToggleIsCustomFormat(true);
            }
        }

        DisableOrEnableSaveButton();
    }

    private void ToggleIsCustomFormat(bool isCustom)
    {
        _isCustomFormat = isCustom;
        CustomFormatRow.Visible = isCustom;
    }
}
