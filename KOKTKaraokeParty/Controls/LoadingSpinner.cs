using Godot;

public partial class LoadingSpinner : TextureProgressBar
{
    public override void _Ready()
    {
        FillMode = (int)FillModeEnum.Clockwise;
        Value = 100;
        var smallestWidth = 60;
        var largestWidth = 200;

        var arcTween = GetTree().CreateTween().SetLoops().SetEase(Tween.EaseType.InOut);
        arcTween.TweenProperty(this, "radial_fill_degrees", largestWidth, 1.25).From(smallestWidth);
        arcTween.TweenProperty(this, "radial_fill_degrees", smallestWidth, 1.25);

        var spinTween = GetTree().CreateTween().SetLoops();
        spinTween.TweenProperty(this, "radial_initial_angle", 360.0, 1).AsRelative();

    }
}
