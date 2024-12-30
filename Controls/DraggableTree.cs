using System.Linq;
using Godot;

public partial class DraggableTree : Tree
{
	public override Variant _GetDragData(Vector2 atPosition)
	{
		var selected = GetSelected();
		if (selected == null)
		{
			return selected;
		}

		DropModeFlags = (int)DropModeFlagsEnum.Inbetween;

		var preview = new Label();
		preview.Text = $"{selected.GetText(0)} - {selected.GetText(1)} - {selected.GetText(2)}";

		SetDragPreview(preview);

		return selected;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
        var draggedItem = data.As<TreeItem>();
		var targetItem = GetItemAtPosition(atPosition);
        var firstItem = GetRoot().GetChildren().FirstOrDefault();

        return draggedItem != null 
        && targetItem != null 
        && targetItem != firstItem // you can't drop on the first item because it's what's currently playing
        && draggedItem != firstItem; // you can't drag the first item because it's what's currently playing
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		var draggedItem = (TreeItem)data;
		var targetItem = GetItemAtPosition(atPosition);
        var dropSection = GetDropSectionAtPosition(atPosition);

		if (targetItem != null && targetItem != draggedItem)
		{
			EmitSignal(SignalName.Reorder, draggedItem.GetMetadata(0), targetItem.GetMetadata(0), dropSection);
		}
	}

	#region Signals
	[Signal]
	public delegate void ReorderEventHandler(string draggedItemMetadata, string targetItemMetadata, int dropSection);
	#endregion
}
