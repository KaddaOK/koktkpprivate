using System;

[Serializable]
public class QueueItem
{
    public string SingerName { get; set; }
    public string SongName { get; set; }
    public string ArtistName { get; set; }
    public string CreatorName { get; set; }
    public string SongInfoLink { get; set; }
    public string PerformanceLink { get; set; }
    public ItemType ItemType { get; set; }
}