using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class LocalScanPathEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("rowid")]
    public int Id { get; set; }
    public string Path { get; set; }
    public string FormatSpecifier { get; set;}
    public DateTime? LastFullScanStarted { get; set; }
    public DateTime? LastFullScanCompleted { get; set; }

    public virtual ICollection<LocalSongFileEntry> Files { get; set; }
}