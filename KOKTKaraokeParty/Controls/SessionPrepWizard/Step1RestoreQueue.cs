using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;
using KOKTKaraokeParty.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KOKTKaraokeParty.Controls.SessionPrepWizard;

public interface IStep1RestoreQueue : IVBoxContainer
{
    event Action<QueueRestoreOption> RestoreChoiceSelected;
    void Setup(SessionPrepWizardState state);
}

[Meta(typeof(IAutoNode))]
public partial class Step1RestoreQueue : VBoxContainer, IStep1RestoreQueue
{
    public override void _Notification(int what) => this.Notify(what);
    
    public event Action<QueueRestoreOption> RestoreChoiceSelected;
    
    #region Nodes
    
    [Node] private ITree RestoreQueueTree { get; set; }
    [Node] private IButton StartFreshButton { get; set; }
    [Node] private IButton YesExceptFirstButton { get; set; }
    [Node] private IButton YesAllButton { get; set; }
    
    #endregion
    
    private SessionPrepWizardState _state;
    
    public void OnReady()
    {
        StartFreshButton.Pressed += () => OnQueueRestoreChoice(QueueRestoreOption.StartFresh);
        YesExceptFirstButton.Pressed += () => OnQueueRestoreChoice(QueueRestoreOption.YesExceptFirst);
        YesAllButton.Pressed += () => OnQueueRestoreChoice(QueueRestoreOption.YesAll);
    }
    
    public void Setup(SessionPrepWizardState state)
    {
        _state = state;
        
        RestoreQueueTree.Clear();
        RestoreQueueTree.Columns = 3;
        RestoreQueueTree.SetColumnTitle(0, "Singer");
        RestoreQueueTree.SetColumnTitle(1, "Song");
        RestoreQueueTree.SetColumnTitle(2, "Artist");
        RestoreQueueTree.SetColumnTitlesVisible(true);
        
        var root = RestoreQueueTree.CreateItem();
        
        foreach (var item in _state.SavedQueueItems)
        {
            var treeItem = RestoreQueueTree.CreateItem(root);
            treeItem.SetText(0, item.SingerName ?? "");
            treeItem.SetText(1, item.SongName ?? "");
            treeItem.SetText(2, item.ArtistName ?? "");
        }
        
        // Disable "Yes, except first" if only one item
        YesExceptFirstButton.Disabled = _state.SavedQueueItems.Count <= 1;
    }
    
    private void OnQueueRestoreChoice(QueueRestoreOption choice)
    {
        RestoreChoiceSelected?.Invoke(choice);
    }
}
