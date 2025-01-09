using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class PerformanceHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("rowid")]
    public int Id { get; set; }

    public DateTime PerformedOn { get; set; }
    public string SingerName { get; set; }
    public string SongName { get; set; }
    public string ArtistName { get; set; }
    public string CreatorName { get; set; }
    public string PerformanceLink { get; set; }

    public int? LocalSongFileEntryId { get; set; }
    public virtual LocalSongFileEntry LocalSongFileEntry { get; set; }
}