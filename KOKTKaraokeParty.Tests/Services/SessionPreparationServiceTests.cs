using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using KOKTKaraokeParty.Services;
using Moq;
using Shouldly;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Tests.Services;

public class SessionPreparationServiceTests(Node testScene) : TestClass(testScene)
{
    private Fixture _fixture = default!;
    private SessionPreparationService _service = default!;
    private Mock<IDisplayScreen> _mockDisplayScreen = default!;
    private Mock<IBrowserProviderNode> _mockBrowserProvider = default!;
    private Mock<IYtDlpProviderNode> _mockYtDlpProvider = default!;
    private Mock<IKarafunRemoteProviderNode> _mockKarafunRemoteProvider = default!;

    [Setup]
    public async Task Setup()
    {
        _fixture = new(TestScene.GetTree());
        _mockDisplayScreen = new Mock<IDisplayScreen>();
        _mockBrowserProvider = new Mock<IBrowserProviderNode>();
        _mockYtDlpProvider = new Mock<IYtDlpProviderNode>();
        _mockKarafunRemoteProvider = new Mock<IKarafunRemoteProviderNode>();
        
        _service = new SessionPreparationService();
        await _fixture.AddToRoot(_service);
        
        _service.Initialize(_mockDisplayScreen.Object, _mockBrowserProvider.Object, _mockYtDlpProvider.Object, _mockKarafunRemoteProvider.Object);
    }

    [Cleanup]
    public async Task Cleanup() => await _fixture.Cleanup();

    private PrepareSessionModel CreateModel(
        VLCStatus vlcStatus = VLCStatus.NotStarted,
        YtDlpStatus ytDlpStatus = YtDlpStatus.NotStarted,
        BrowserAvailabilityStatus browserStatus = BrowserAvailabilityStatus.NotStarted,
        YouTubeStatus youTubeStatus = YouTubeStatus.NotStarted,
        KarafunStatus karafunStatus = KarafunStatus.NotStarted)
    {
        return new PrepareSessionModel
        {
            VLCStatus = vlcStatus,
            YtDlpStatus = ytDlpStatus,
            BrowserStatus = browserStatus,
            YouTubeStatus = youTubeStatus,
            KarafunStatus = karafunStatus
        };
    }

    [Test]
    public void IsYouTubeUsable_ReturnsFalse_WhenAllServicesNotReady()
    {
        // Arrange
        var model = CreateModel();

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsYouTubeUsable_ReturnsTrue_WhenVlcAndYtDlpReady()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.Ready
        );

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsYouTubeUsable_ReturnsTrue_WhenBrowserAndYouTubePremiumReady()
    {
        // Arrange
        var model = CreateModel(
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.Premium
        );

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsYouTubeUsable_ReturnsTrue_WhenBrowserAndYouTubeNotPremiumReady()
    {
        // Arrange
        var model = CreateModel(
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.NotPremium
        );

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsYouTubeUsable_ReturnsFalse_WhenOnlyVlcReady()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.Ready);

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsYouTubeUsable_ReturnsFalse_WhenOnlyYtDlpReady()
    {
        // Arrange
        var model = CreateModel(ytDlpStatus: YtDlpStatus.Ready);

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsYouTubeUsable_ReturnsFalse_WhenOnlyBrowserReady()
    {
        // Arrange
        var model = CreateModel(browserStatus: BrowserAvailabilityStatus.Ready);

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsYouTubeUsable_ReturnsFalse_WhenYouTubeNotLoggedIn()
    {
        // Arrange
        var model = CreateModel(
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.NotLoggedIn
        );

        // Act
        var result = _service.IsYouTubeUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsKarafunUsable_ReturnsTrue_WhenBrowserAndKarafunActive()
    {
        // Arrange
        var model = CreateModel(
            browserStatus: BrowserAvailabilityStatus.Ready,
            karafunStatus: KarafunStatus.Active
        );

        // Act
        var result = _service.IsKarafunUsable(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsKarafunUsable_ReturnsFalse_WhenKarafunNotActive()
    {
        // Arrange
        var model = CreateModel(
            browserStatus: BrowserAvailabilityStatus.Ready,
            karafunStatus: KarafunStatus.NotLoggedIn
        );

        // Act
        var result = _service.IsKarafunUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsKarafunUsable_ReturnsFalse_WhenBrowserNotReady()
    {
        // Arrange
        var model = CreateModel(
            browserStatus: BrowserAvailabilityStatus.Checking,
            karafunStatus: KarafunStatus.Active
        );

        // Act
        var result = _service.IsKarafunUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsLocalFilesUsable_ReturnsTrue_WhenVlcReady()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.Ready);

        // Act
        var result = _service.IsLocalFilesUsable(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsLocalFilesUsable_ReturnsFalse_WhenVlcNotReady()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.Initializing);

        // Act
        var result = _service.IsLocalFilesUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsLocalFilesUsable_ReturnsFalse_WhenVlcFatalError()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.FatalError);

        // Act
        var result = _service.IsLocalFilesUsable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void MultipleServicesCanBeUsableSimultaneously()
    {
        // Arrange - All services ready
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.Ready,
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.Premium,
            karafunStatus: KarafunStatus.Active
        );

        // Act & Assert
        _service.IsLocalFilesUsable(model).ShouldBeTrue();
        _service.IsYouTubeUsable(model).ShouldBeTrue();
        _service.IsKarafunUsable(model).ShouldBeTrue();
    }

    [Test]
    public void PartialServicesAvailable_LocalOnlyScenario()
    {
        // Arrange - Only VLC ready, browser services failed
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.FatalError,
            browserStatus: BrowserAvailabilityStatus.FatalError
        );

        // Act & Assert
        _service.IsLocalFilesUsable(model).ShouldBeTrue();
        _service.IsYouTubeUsable(model).ShouldBeFalse();
        _service.IsKarafunUsable(model).ShouldBeFalse();
    }

    [Test]
    public void PartialServicesAvailable_YouTubeViaDownloadOnly()
    {
        // Arrange - VLC + YtDlp ready, browser failed
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.Ready,
            browserStatus: BrowserAvailabilityStatus.FatalError
        );

        // Act & Assert
        _service.IsLocalFilesUsable(model).ShouldBeTrue();
        _service.IsYouTubeUsable(model).ShouldBeTrue();
        _service.IsKarafunUsable(model).ShouldBeFalse();
    }

    [Test]
    public void PartialServicesAvailable_StreamingOnlyScenario()
    {
        // Arrange - Browser services ready, VLC failed
        var model = CreateModel(
            vlcStatus: VLCStatus.FatalError,
            ytDlpStatus: YtDlpStatus.FatalError,
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.Premium,
            karafunStatus: KarafunStatus.Active
        );

        // Act & Assert
        _service.IsLocalFilesUsable(model).ShouldBeFalse();
        _service.IsYouTubeUsable(model).ShouldBeTrue();
        _service.IsKarafunUsable(model).ShouldBeTrue();
    }
}
