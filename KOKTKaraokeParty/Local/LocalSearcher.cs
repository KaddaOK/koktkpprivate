using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

public interface ILocalSearcher
{
    Task<List<LocalSongFileEntry>> Search(string query, CancellationToken cancellationToken);
}

public class LocalSearcher : ILocalSearcher
{
    #region Initialized Dependencies

    private KOKTDbContext DbContext { get; set; }

    public LocalSearcher(KOKTDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public LocalSearcher() : this(new KOKTDbContext()) {
        DbContext.Database.EnsureCreated();
    }

    #endregion

    public async Task<List<LocalSongFileEntry>> Search(string query, CancellationToken cancellationToken)
    {
        var queryLower = query.ToLower(); // TODO: filter more?
        return await DbContext.LocalSongFiles
        .Where(song => song.FileNameWithoutExtension.ToLower().Contains(queryLower))
        .ToListAsync(cancellationToken);
    }
}