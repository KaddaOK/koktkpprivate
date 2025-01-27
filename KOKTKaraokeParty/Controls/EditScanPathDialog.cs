using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using System;

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

    #region Nodes
    [Node] private ILabel SelectedPathLabel { get; set; } = default!;
    [Node] private IButton BrowsePathButton { get; set; } = default!;
    [Node] private IOptionButton FormatOptionButton { get; set; } = default!;
    [Node] private IFileDialog EditPathFileDialog { get; set; } = default!;
    [Node] private ILabel CustomFormatHeadingLabel { get; set; } = default!;
    [Node] private IVBoxContainer CustomFormatVBoxContainer { get; set; } = default!;
    [Node] private ILineEdit CustomFormatLineEdit { get; set; } = default!;
    [Node] private IAcceptDialog CustomFormatValidationErrorDialog { get; set; } = default!;
    [Node] private ILabel CustomFormatErrorLabel { get; set; } = default!;
    #endregion

    #region Signals
    [Signal]
    public delegate void ScanPathSavedEventHandler(int scanPathEntryId);
    #endregion

    #region Initialized Dependencies

    private ILocalFileNameMetadataParser MetadataParser { get; set; }
    private KOKTDbContext DbContext { get; set; }

    public void SetupForTesting(ILocalFileNameMetadataParser parser, KOKTDbContext dbContext)
    {
        MetadataParser = parser;
        DbContext = dbContext;
    }

    public void Initialize()
    {
        MetadataParser = new LocalFileNameMetadataParser();

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
            CustomFormatErrorLabel.Text = "";
        };
        DisableOrEnableSaveButton();
    }

    private void DisableOrEnableSaveButton()
    {
        var okButton = GetOkButton();
        if (okButton != null)
        {
            okButton.Disabled = 
                string.IsNullOrWhiteSpace(SelectedPathLabel.Text) || 
                (!_isCustomFormat && FormatOptionButton.Selected == -1) ||
                (_isCustomFormat && string.IsNullOrWhiteSpace(CustomFormatLineEdit.Text));
        }
    }

    private void SaveButtonPressed()
    {
        if (string.IsNullOrWhiteSpace(SelectedPathLabel.Text))
        {
            return;
        }

        if (_isCustomFormat)
        {
            var isValid = MetadataParser.ValidateFormatSpecification(CustomFormatLineEdit.Text);
            if (!isValid.isValid)
            {
                CustomFormatErrorLabel.Text = isValid.validationError;
                CustomFormatValidationErrorDialog.Show();
                return;
            }
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
        _scanPathEntryToBeEdited.FormatSpecifier = _isCustomFormat ? CustomFormatLineEdit.Text : FormatOptionButton.GetItemText(FormatOptionButton.Selected);
        DbContext.SaveChanges();
        EmitSignal(SignalName.ScanPathSaved, _scanPathEntryToBeEdited.Id);
    }


    private void EditPathFileDialogDirSelected(string dir)
    {
        SelectedPathLabel.Text = dir;
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

    public void SetScanPathEntry(LocalScanPathEntry scanPathEntry)
    {
        _scanPathEntryToBeEdited = scanPathEntry;
        SelectedPathLabel.Text = _scanPathEntryToBeEdited?.Path;
        CustomFormatLineEdit.Text = _scanPathEntryToBeEdited?.FormatSpecifier;
        EditPathFileDialog.CurrentDir = _scanPathEntryToBeEdited?.Path;
        
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
        CustomFormatHeadingLabel.Visible = isCustom;
        CustomFormatVBoxContainer.Visible = isCustom;
    }
}
