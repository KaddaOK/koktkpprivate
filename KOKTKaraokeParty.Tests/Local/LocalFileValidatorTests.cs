namespace KOKTKaraokeParty.Tests.Local;

using Moq;
using Xunit;

public class LocalFileValidatorTests
{
    private Mock<IFileWrapper> _fileWrapper;
    private Mock<IZipFileManager> _zipManager;
    private Mock<IZipArchiveEntry> testCdgEntry;
    private Mock<IZipArchiveEntry> testMp3Entry;
    private readonly LocalFileValidator _validator;
    public LocalFileValidatorTests()
    {
        _fileWrapper = new Mock<IFileWrapper>();
        _zipManager = new Mock<IZipFileManager>();

        testCdgEntry = new Mock<IZipArchiveEntry>();
        testCdgEntry.Setup(x => x.FullName).Returns("test.cdg").Verifiable();
        testMp3Entry = new Mock<IZipArchiveEntry>();
        testMp3Entry.Setup(x => x.FullName).Returns("test.mp3").Verifiable();

        _validator = new LocalFileValidator(_fileWrapper.Object, _zipManager.Object);
    }

    [Fact]
    public void IsValid_FileDoesNotExist_ReturnsFalse()
    {
        _fileWrapper.Setup(x => x.Exists("nonexistentfile.cdg")).Returns(false);

        var result = _validator.IsValid("nonexistentfile.cdg");

        Assert.False(result.isValid);
        Assert.Equal("nonexistentfile.cdg does not exist.", result.message);
    }

    [Fact]
    public void IsValid_UnexpectedFileExtension_ReturnsFalse()
    {
        _fileWrapper.Setup(x => x.Exists("file.txt")).Returns(true).Verifiable();

        var result = _validator.IsValid("file.txt");

        Assert.False(result.isValid);
        Assert.Equal("Unexpected file extension for file.txt.", result.message);
    }

    [Fact]
    public void IsValid_CdgFileWithoutMatchingMp3_ReturnsFalse()
    {
        var cdgFilePath = "//path/to/test_file.cdg";
        _fileWrapper.Setup(x => x.Exists(cdgFilePath)).Returns(true).Verifiable();
        _fileWrapper.Setup(x => x.Exists("//path/to/test_file.mp3")).Returns(false).Verifiable();

        var result = _validator.IsValid(cdgFilePath);

        Assert.False(result.isValid);
        Assert.Equal("No matching MP3 file found beside 'test_file.cdg'.", result.message);
        _fileWrapper.Verify();
    }

    [Fact]
    public void IsValid_Mp3FileWithoutMatchingCdg_ReturnsFalse()
    {
        var mp3FilePath = "//path/to/test_file/test_q.mp3";
        _fileWrapper.Setup(x => x.Exists(mp3FilePath)).Returns(true).Verifiable();
        _fileWrapper.Setup(x => x.Exists("//path/to/test_file/test_q.cdg")).Returns(false).Verifiable();

        var result = _validator.IsValid(mp3FilePath);
        Assert.False(result.isValid);
        Assert.Equal("No matching CDG file found beside 'test_q.mp3'.", result.message);
        _fileWrapper.Verify();
    }

    [Fact]
    public void IsValid_ValidCdgAndMp3Pair_ReturnsTrue()
    {
        var cdgFilePath = @"\\some\path\test.cdg";
        var mp3FilePath = @"\\some\path\test.mp3";
        _fileWrapper.Setup(x => x.Exists(cdgFilePath)).Returns(true).Verifiable();
        _fileWrapper.Setup(x => x.Exists(mp3FilePath)).Returns(true).Verifiable();

        var result = _validator.IsValid(cdgFilePath);

        Assert.Null(result.message);
        Assert.True(result.isValid);
        _fileWrapper.Verify();
    }

    [Fact]
    public void IsValid_ValidMp4File_ReturnsTrue()
    {
        var mp4FilePath = "/var/etc/test.mp4";
        _fileWrapper.Setup(x => x.Exists(mp4FilePath)).Returns(true).Verifiable();

        var result = _validator.IsValid(mp4FilePath);

        Assert.Null(result.message);
        Assert.True(result.isValid);
        _fileWrapper.Verify();
    }

    [Fact]
    public void IsValid_ValidZipFileWithMatchingCdgAndMp3_ReturnsTrue()
    {
        var zipFilePath = "test.zip";
        _fileWrapper.Setup(x => x.Exists(zipFilePath)).Returns(true).Verifiable();

        var zipArchive = new Mock<IZipArchive>();
        zipArchive.Setup(x => x.Entries).Returns(new List<IZipArchiveEntry> { testCdgEntry.Object, testMp3Entry.Object }.AsReadOnly()).Verifiable();
        _zipManager.Setup(x => x.OpenRead(zipFilePath)).Returns(zipArchive.Object).Verifiable();

        var result = _validator.IsValid(zipFilePath);

        Assert.Null(result.message);
        Assert.True(result.isValid);

        _fileWrapper.Verify();
        _zipManager.Verify();
        zipArchive.Verify();
        testCdgEntry.Verify();
        testMp3Entry.Verify();
    }

    [Fact]
    public void IsValid_ZipFileWithoutCdgFile_ReturnsFalse()
    {
        var zipFilePath = "test.zip";
        _fileWrapper.Setup(x => x.Exists(zipFilePath)).Returns(true).Verifiable();

        var zipArchive = new Mock<IZipArchive>();
        zipArchive.Setup(x => x.Entries).Returns(new List<IZipArchiveEntry> { testMp3Entry.Object }.AsReadOnly()).Verifiable();
        _zipManager.Setup(x => x.OpenRead(zipFilePath)).Returns(zipArchive.Object).Verifiable();

        var result = _validator.IsValid(zipFilePath);

        Assert.Equal("'test.zip' does not contain a .cdg and .mp3 file.", result.message);
        Assert.False(result.isValid);

        _fileWrapper.Verify();
        _zipManager.Verify();
        zipArchive.Verify();
        testMp3Entry.Verify();
    }

    [Fact]
    public void IsValid_ZipFileWithoutMp3File_ReturnsFalse()
    {
        var zipFilePath = "test.zip";
        _fileWrapper.Setup(x => x.Exists(zipFilePath)).Returns(true).Verifiable();

        var zipArchive = new Mock<IZipArchive>();
        zipArchive.Setup(x => x.Entries).Returns(new List<IZipArchiveEntry> { testCdgEntry.Object }.AsReadOnly()).Verifiable();
        _zipManager.Setup(x => x.OpenRead(zipFilePath)).Returns(zipArchive.Object).Verifiable();

        var result = _validator.IsValid(zipFilePath);

        Assert.Equal("'test.zip' does not contain a .cdg and .mp3 file.", result.message);
        Assert.False(result.isValid);

        _fileWrapper.Verify();
        _zipManager.Verify();
        zipArchive.Verify();
        testCdgEntry.Verify();
    }

    [Fact]
    public void IsValid_ZipFileContentsNamesDoNotMatch_ReturnsTrueWithWarning()
    {
        var zipFilePath = "test.zip";
        _fileWrapper.Setup(x => x.Exists(zipFilePath)).Returns(true).Verifiable();

        var zipArchive = new Mock<IZipArchive>();
        zipArchive.Setup(x => x.Entries).Returns(new List<IZipArchiveEntry> { testCdgEntry.Object, testMp3Entry.Object }.AsReadOnly()).Verifiable();
        _zipManager.Setup(x => x.OpenRead(zipFilePath)).Returns(zipArchive.Object).Verifiable();

        testCdgEntry.Setup(x => x.FullName).Returns("test1.cdg").Verifiable();
        testMp3Entry.Setup(x => x.FullName).Returns("test2.mp3").Verifiable();

        var result = _validator.IsValid(zipFilePath);

        Assert.Equal("test.zip names do not match (first CDG entry was 'test1.cdg' but first MP3 entry was 'test2.mp3').", result.message);
        Assert.True(result.isValid);

        _fileWrapper.Verify();
        _zipManager.Verify();
        zipArchive.Verify();
        testCdgEntry.Verify();
        testMp3Entry.Verify();
    }
}