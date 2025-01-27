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
                "KFS-00602 - Morgan Wallen - Thinkin' Bout Me (Parody).mp4", "~|KFS-00602|Morgan Wallen|Thinkin' Bout Me (Parody)")]
    [InlineData("{title} - {artist}", 
                "Hello - Adele.zip", "~|~|Adele|Hello")]
    [InlineData("{artist}_{title}_{creator}_{identifier}", 
                "Awful artist_a test song_bad taste media_whotfknows-88.cdg", "bad taste media|whotfknows-88|Awful artist|a test song")]
    public void CanParse(string formatSpecification, string fileName, string expectedResultPipeDelimitedNullToTilde)
    {
        var results = _parser.Parse(fileName, formatSpecification);
        Assert.Equal(expectedResultPipeDelimitedNullToTilde, PipeDelimitedNullToTilde(results));
    }

    [Theory]
    [InlineData("{creator}/{artist} > {title}", 
                @"numerous\folders\along\Some person > needs help.cdg", "along|~|Some person|needs help")]
    [InlineData("{creator}/**/{artist} ? {title}", 
                @"numerous\folders\along\Some person ? needs help badly.cdg", "numerous|~|Some person|needs help badly")]
    [InlineData("{creator}/**/{artist} ? {title}", 
                @"\numerous\folders\along\Some person ? needs help badly.cdg", "numerous|~|Some person|needs help badly")]
    [InlineData("{creator}/*/skip/*/{artist}/*/{title} - {identifier}", 
                @"//ignore most/creative/folder/skip/junk/here/not/trouble - 999.mp3", "creative|999|here|trouble")]
    public void CanFoldersParticipate(string formatSpecification, string fileName, string expectedResultPipeDelimitedNullToTilde)
    {
        var results = _parser.Parse(fileName, formatSpecification);
        Assert.Equal(expectedResultPipeDelimitedNullToTilde, PipeDelimitedNullToTilde(results));
    }

    [Theory]
    [InlineData(@"/*/{creator}/**/{identifier} - {artist} - {title}", 
                @"/diveBar/Aghastronaut's Karaoke From Space MP4/KFS 0501-1000/KFS-00607 - Belle and Sebastian - Funny Little Frog.mp4", 
                "Aghastronaut's Karaoke From Space MP4|KFS-00607|Belle and Sebastian|Funny Little Frog")]
    [InlineData(@"/*/{creator}/**/{identifier} - {artist} - {title}", 
                @"diveBar/Aghastronaut's Karaoke From Space MP4/KFS 0501-1000/KFS-00607 - Belle and Sebastian - Funny Little Frog.mp4", 
                "Aghastronaut's Karaoke From Space MP4|KFS-00607|Belle and Sebastian|Funny Little Frog")]
    [InlineData(@"/*/{creator}/**/{identifier} - {artist} - {title}", 
                @"\diveBar\Andrew The Nomad MP4\MP4\NOMAD 0501-0750\NOMAD-0674 - Alabama - The Fans.mp4", 
                "Andrew The Nomad MP4|NOMAD-0674|Alabama|The Fans")]
    [InlineData(@"/*/{creator}/**/{identifier} - {artist} - {title}", 
                @"diveBar\Andrew The Nomad MP4\MP4\NOMAD 0501-0750\NOMAD-0674 - Alabama - The Fans.mp4", 
                "Andrew The Nomad MP4|NOMAD-0674|Alabama|The Fans")]
    [InlineData(@"/*/{creator}/**/{identifier} - {artist} - {title}", 
                @"\Other well-known creators\2RK 2 Reel Karaoke Entertainment Homemade\2RK0009\2RK0009-01 - Tragically Hip - In View.zip", 
                "2RK 2 Reel Karaoke Entertainment Homemade|2RK0009-01|Tragically Hip|In View")]
    [InlineData(@"/*/{creator}/**/{identifier} - {artist} - {title}", 
                @"Other well-known creators\2RK 2 Reel Karaoke Entertainment Homemade\2RK0009\2RK0009-01 - Tragically Hip - In View.zip", 
                "2RK 2 Reel Karaoke Entertainment Homemade|2RK0009-01|Tragically Hip|In View")]
    [InlineData(@"*/{creator}/**/{identifier} - {artist} - {title}", 
                @"Other well-known creators\2RK 2 Reel Karaoke Entertainment Homemade\2RK0009\2RK0009-01 - Tragically Hip - In View.zip", 
                "2RK 2 Reel Karaoke Entertainment Homemade|2RK0009-01|Tragically Hip|In View")]
    public void VariableNumberOfFoldersIsAGodDamnNightmare(string formatSpecification, string fileName, string expectedResultPipeDelimitedNullToTilde)
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
    [InlineData("test/*/**/stuff")]
    [InlineData("test/**/*/stuff")]
    public void ShouldRejectSingleAndDoubleFolderWildcardsAdjacent(string formatSpecification)
    {
        Assert.Throws<ArgumentException>(() => _parser.GetRegexEquivalent(formatSpecification));
    }

    [Theory]
    [InlineData("test/**/**/stuff")]
    [InlineData("test/**/my/**/stuff")]
    public void ShouldRejectMoreThanOneDoubleAsteriskWildcard(string formatSpecification)
    {
        Assert.Throws<ArgumentException>(() => _parser.GetRegexEquivalent(formatSpecification));
    }

    [Fact]
    public void ShouldRejectTooManyAsterisksInFolderWildcard()
    {
        Assert.Throws<ArgumentException>(() => _parser.GetRegexEquivalent("test/***/stuff"));
    }

        [Fact]
    public void ShouldRejectTooManyAsterisksInRegularWildcard()
    {
        Assert.Throws<ArgumentException>(() => _parser.GetRegexEquivalent("test/**stuff"));
    }

    [Theory]
    [InlineData("blah {token}* blah")]
    [InlineData("blah *{token} blah")]
    public void ShouldRejectWildcardsNextToTokens(string formatSpecification)
    {
        Assert.Throws<ArgumentException>(() => _parser.GetRegexEquivalent(formatSpecification));
    }
}