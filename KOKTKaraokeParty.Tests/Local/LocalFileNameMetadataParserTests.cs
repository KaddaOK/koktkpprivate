using Xunit;

public class LocalFileNameMetadataParserTests
{
    private readonly LocalFileNameMetadataParser _parser;
    public LocalFileNameMetadataParserTests()
    {
        _parser = new LocalFileNameMetadataParser();
    }

    private string PipeDelimitedNullToTilde(SongMetadata results)
    {
        return $"{results.CreatorName ?? "~"}|{results.Identifier ?? "~"}|{results.ArtistName ?? "~"}|{results.SongTitle ?? "~"}";
    }

    [Theory]
    [InlineData("{identifier} - {artist} - {title}", 
                "KFS-00602 - Morgan Wallen - Thinkin' Bout Me (Parody).mp4", 
                "~|KFS-00602|Morgan Wallen|Thinkin' Bout Me (Parody)")]
    [InlineData("{title} - {artist}", 
                "Hello - Adele.zip", 
                "~|~|Adele|Hello")]
    [InlineData("{artist}_{title}_{creator}_{identifier}", 
                "Awful artist_a test song_bad taste media_whotfknows-88.cdg", 
                "bad taste media|whotfknows-88|Awful artist|a test song")]
    public void CanParse(string formatSpecification, string fileName, string expectedResultPipeDelimitedNullToTilde)
    {
        var results = _parser.Parse(fileName, formatSpecification);
        Assert.Equal(expectedResultPipeDelimitedNullToTilde, PipeDelimitedNullToTilde(results));
    }

    [Theory]
    [InlineData("{creator}/{artist} > {title}", 
                @"numerous\folders\along\Some person > needs help.cdg", 
                "along|~|Some person|needs help")]
    [InlineData("{creator}/**/{artist} ? {title}", 
                @"numerous\folders\along\Some person ? needs help badly.cdg", 
                "numerous|~|Some person|needs help badly")]
    [InlineData("{creator}/*/skip/*/{artist}/*/{title} - {identifier}", 
                @"//ignore most/creative/folder/skip/junk/here/not/trouble - 999.mp3", 
                "creative|999|here|trouble")]
    public void CanFoldersParticipate(string formatSpecification, string fileName, string expectedResultPipeDelimitedNullToTilde)
    {
        var results = _parser.Parse(fileName, formatSpecification);
        Assert.Equal(expectedResultPipeDelimitedNullToTilde, PipeDelimitedNullToTilde(results));
    }

    [Theory]
    [InlineData(@"{creator}/*-{identifier} - {artist} and * {title} - The *", 
                @"C:\whatever\CLOAKY-026 - Siouxsie and The Banshees - The Passenger.zip", 
                "whatever|026|Siouxsie|Banshees")]
    public void ShouldExpandWildcards(string formatSpecification, string fileName, string expectedResultPipeDelimitedNullToTilde)
    {
        var results = _parser.Parse(fileName, formatSpecification);
        Assert.Equal(expectedResultPipeDelimitedNullToTilde, PipeDelimitedNullToTilde(results));
    }

    [Theory]
    [InlineData("test/*/*/stuff")]
    [InlineData("test/*/**/stuff")]
    [InlineData("test/**/*/stuff")]
    [InlineData("test/**/**/stuff")]
    public void ShouldRejectAdjacentFolderWildcards(string formatSpecification)
    {
        Assert.Throws<ArgumentException>(() => _parser.GetRegexEquivalent(formatSpecification));
    }

    [Theory]
    [InlineData("blah {token}* blah")]
    [InlineData("blah *{token} blah")]
    public void ShouldRejectWildcardsNextToTokens(string formatSpecification)
    {
        Assert.Throws<ArgumentException>(() => _parser.GetRegexEquivalent(formatSpecification));
    }
}