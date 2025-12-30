using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotNodeInterfaces;
using Godot;
using KOKTKaraokeParty.Services;
using Moq;
using Shouldly;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Tests.Services;

public class SessionUIServiceTests(Node testScene) : TestClass(testScene)
{
    private Fixture _fixture = default!;
    private SessionUIService _service = default!;
    private Mock<SessionPreparationService> _mockSessionPreparation = default!;

    [Setup]
    public async Task Setup()
    {
        _fixture = new(TestScene.GetTree());
        _mockSessionPreparation = new Mock<SessionPreparationService>();
        _service = new SessionUIService();
        
        await _fixture.AddToRoot(_service);
        _service.Initialize(_mockSessionPreparation.Object);
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
    public void IsAnyCheckInProgress_ReturnsTrue_WhenBrowserChecking()
    {
        // Arrange
        var model = CreateModel(browserStatus: BrowserAvailabilityStatus.Checking);

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsTrue_WhenBrowserDownloading()
    {
        // Arrange
        var model = CreateModel(browserStatus: BrowserAvailabilityStatus.Downloading);

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsTrue_WhenYouTubeChecking()
    {
        // Arrange
        var model = CreateModel(youTubeStatus: YouTubeStatus.Checking);

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsTrue_WhenKarafunChecking()
    {
        // Arrange
        var model = CreateModel(karafunStatus: KarafunStatus.Checking);

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsTrue_WhenVlcInitializing()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.Initializing);

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsTrue_WhenYtDlpChecking()
    {
        // Arrange
        var model = CreateModel(ytDlpStatus: YtDlpStatus.Checking);

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsTrue_WhenYtDlpDownloading()
    {
        // Arrange
        var model = CreateModel(ytDlpStatus: YtDlpStatus.Downloading);

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsFalse_WhenAllChecksComplete()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.Ready,
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.Premium,
            karafunStatus: KarafunStatus.Active
        );

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void IsAnyCheckInProgress_ReturnsFalse_WhenAllChecksFailed()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.FatalError,
            ytDlpStatus: YtDlpStatus.FatalError,
            browserStatus: BrowserAvailabilityStatus.FatalError,
            youTubeStatus: YouTubeStatus.FatalError,
            karafunStatus: KarafunStatus.FatalError
        );

        // Act
        var result = _service.IsAnyCheckInProgress(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void AreAllServicesUnusable_ReturnsTrue_WhenAllServicesFailed()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.FatalError,
            browserStatus: BrowserAvailabilityStatus.FatalError
        );

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        var result = _service.AreAllServicesUnusable(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void AreAllServicesUnusable_ReturnsFalse_WhenLocalFilesUsable()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.Ready);

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        var result = _service.AreAllServicesUnusable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void AreAllServicesUnusable_ReturnsFalse_WhenYouTubeUsable()
    {
        // Arrange
        var model = CreateModel();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        var result = _service.AreAllServicesUnusable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void AreAllServicesUnusable_ReturnsFalse_WhenKarafunUsable()
    {
        // Arrange
        var model = CreateModel();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(true);

        // Act
        var result = _service.AreAllServicesUnusable(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void AreAllServicesReady_ReturnsTrue_WhenAllServicesUsable()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.Ready,
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.Premium,
            karafunStatus: KarafunStatus.Active
        );

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(true);

        // Act
        var result = _service.AreAllServicesReady(model);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public void AreAllServicesReady_ReturnsFalse_WhenLocalFilesNotUsable()
    {
        // Arrange
        var model = CreateModel();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(true);

        // Act
        var result = _service.AreAllServicesReady(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void AreAllServicesReady_ReturnsFalse_WhenYouTubeNotUsable()
    {
        // Arrange
        var model = CreateModel();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(true);

        // Act
        var result = _service.AreAllServicesReady(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void AreAllServicesReady_ReturnsFalse_WhenKarafunNotUsable()
    {
        // Arrange
        var model = CreateModel();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        var result = _service.AreAllServicesReady(model);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void UpdateButtonStates_DisablesOkButton_WhenNothingUsable()
    {
        // Arrange
        var model = CreateModel();
        var mockOkButton = new Mock<IButton>();
        var mockLoginButton = new Mock<IButton>();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        _service.UpdateButtonStates(model, mockOkButton.Object, mockLoginButton.Object);

        // Assert
        mockOkButton.VerifySet(b => b.Disabled = true);
    }

    [Test]
    public void UpdateButtonStates_EnablesOkButton_WhenAtLeastOneServiceUsable()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.Ready);
        var mockOkButton = new Mock<IButton>();
        var mockLoginButton = new Mock<IButton>();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        _service.UpdateButtonStates(model, mockOkButton.Object, mockLoginButton.Object);

        // Assert
        mockOkButton.VerifySet(b => b.Disabled = false);
        mockOkButton.VerifySet(b => b.Text = "Start the Party!");
    }

    [Test]
    public void UpdateButtonStates_SetsWarningTooltip_WhenSomeServicesNotReady()
    {
        // Arrange
        var model = CreateModel(vlcStatus: VLCStatus.Ready);
        var mockOkButton = new Mock<IButton>();
        var mockLoginButton = new Mock<IButton>();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        _service.UpdateButtonStates(model, mockOkButton.Object, mockLoginButton.Object);

        // Assert
        mockOkButton.VerifySet(b => b.TooltipText = It.Is<string>(s => s.Contains("YouTube not available")));
        mockOkButton.VerifySet(b => b.TooltipText = It.Is<string>(s => s.Contains("Karafun not available")));
    }

    [Test]
    public void UpdateButtonStates_DisablesLoginButton_WhileChecking()
    {
        // Arrange
        var model = CreateModel(browserStatus: BrowserAvailabilityStatus.Checking);
        var mockOkButton = new Mock<IButton>();
        var mockLoginButton = new Mock<IButton>();

        // Act
        _service.UpdateButtonStates(model, mockOkButton.Object, mockLoginButton.Object);

        // Assert
        mockLoginButton.VerifySet(b => b.Disabled = true);
    }

    [Test]
    public void PopulateServiceConfirmationDialog_IncludesAllServices()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            browserStatus: BrowserAvailabilityStatus.FatalError
        );
        var mockLabel = new Mock<ILabel>();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(false);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        _service.PopulateServiceConfirmationDialog(mockLabel.Object, model);

        // Assert
        mockLabel.VerifySet(l => l.Text = It.Is<string>(s => 
            s.Contains("✔ Local Files") &&
            s.Contains("❌ YouTube") &&
            s.Contains("❌ Karafun") &&
            s.Contains("Not all services are ready") &&
            s.Contains("Are you sure you wish to continue anyway?")));
    }

    [Test]
    public void PopulateServiceConfirmationDialog_ShowsMultipleAvailableServices()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.Ready,
            browserStatus: BrowserAvailabilityStatus.FatalError
        );
        var mockLabel = new Mock<ILabel>();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(false);

        // Act
        _service.PopulateServiceConfirmationDialog(mockLabel.Object, model);

        // Assert
        mockLabel.VerifySet(l => l.Text = It.Is<string>(s => 
            s.Contains("✔ Local Files") &&
            s.Contains("✔ YouTube") &&
            s.Contains("❌ Karafun")));
    }

    [Test]
    public void PopulateServiceConfirmationDialog_ShowsAllAvailableWhenFullyReady()
    {
        // Arrange
        var model = CreateModel(
            vlcStatus: VLCStatus.Ready,
            ytDlpStatus: YtDlpStatus.Ready,
            browserStatus: BrowserAvailabilityStatus.Ready,
            youTubeStatus: YouTubeStatus.Premium,
            karafunStatus: KarafunStatus.Active
        );
        var mockLabel = new Mock<ILabel>();

        _mockSessionPreparation.Setup(s => s.IsLocalFilesUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsYouTubeUsable(model)).Returns(true);
        _mockSessionPreparation.Setup(s => s.IsKarafunUsable(model)).Returns(true);

        // Act
        _service.PopulateServiceConfirmationDialog(mockLabel.Object, model);

        // Assert
        mockLabel.VerifySet(l => l.Text = It.Is<string>(s => 
            s.Contains("✔ Local Files") &&
            s.Contains("✔ YouTube") &&
            s.Contains("✔ Karafun")));
    }
}
