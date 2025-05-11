using System;
using Godot;

[Serializable]
public partial class QueueItem : GodotObject
{
    public string SingerName { get; set; }
    public string SongName { get; set; }
    public string ArtistName { get; set; }
    public string CreatorName { get; set; }
    public string Identifier { get; set; }
    public string SongInfoLink { get; set; }
    public string PerformanceLink { get; set; }
    public ItemType ItemType { get; set; }
}