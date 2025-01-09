using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class LocalSongFileEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("rowid")]
    public int Id { get; set; }

    public string FullPath { get; set; }

    public string FileNameWithoutExtension { get; set; }

    public string ArtistName { get; set; }
    public string SongName { get; set; }
    public string CreatorName { get; set; }
    public string Identifier { get; set; }
    
    public int PerformedCount { get; set; }
    public DateTime? FirstPerformedOn { get; set; }
    public string FirstPerformedBy { get; set; }
    public DateTime? LastPerformedOn { get; set; }
    public string LastPerformedBy { get; set; }

    public bool? IsCustomized { get; set; }

    public int? ParentPathId { get; set; }
    public virtual LocalScanPathEntry ParentPath { get; set; }

    public virtual ICollection<PerformanceHistory> Performances { get; set; }
}
/*
public class LocalSongFileEntryFTS
{
    [Key]
    public int RowId { get; set; }
    public decimal? Rank { get; set; }
    [Column(nameof(LocalSongFileEntryFTS))]
    public string Match { get; set; }

    public LocalSongFileEntry LocalSongFileEntry { get; set; }

    public string ArtistName { get; set; }
    public string SongName { get; set; }
    public string Source { get; set; }
}*/