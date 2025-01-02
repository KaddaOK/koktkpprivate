using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface ILoadableLabel : IControl
{
    void SetLoaded(bool isLoaded, string text = null);
}

[Meta(typeof(IAutoNode))]
public partial class LoadableLabel : Control, ILoadableLabel
{
    public override void _Notification(int what) => this.Notify(what);

    [Export] public int FontSize {get; set; } = 20;

    [Node] private Label LoadedLabel { get; set; } = default!;
    [Node] private LoadingSpinner LoadingSpinner { get; set; } = default!;

    public void OnReady()
    {
        SetLoaded(false);
        //GD.Print($"LoadableLabel.OnReady() FontSize: {FontSize}");
        // Ensure the label has a valid theme
        if (LoadedLabel.Theme == null)
        {
            LoadedLabel.Theme = new Theme();
        }

        // Apply the font size override
        LoadedLabel.AddThemeFontSizeOverride("font_size", FontSize);
    }

    public void SetLoaded(bool isLoaded, string text = null)
    {
        //GD.Print($"LoadableLabel.SetLoaded({isLoaded}, {text})");
        LoadedLabel.Text = text;
        LoadingSpinner.Visible = !isLoaded;
        LoadedLabel.Visible = isLoaded;
    }
}
