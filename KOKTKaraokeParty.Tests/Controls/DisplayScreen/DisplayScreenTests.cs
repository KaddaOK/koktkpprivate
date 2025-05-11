namespace KOKTKaraokeParty.Tests.Controls;

using System.Threading.Tasks;
using Chickensoft.AutoInject;
using Chickensoft.GoDotTest;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.GodotTestDriver;
using Godot;
using Moq;

public class DisplayScreenTests(Node testScene) : TestClass(testScene)
{
    private Fixture _fixture = default!;
    private DisplayScreen _scene = default!;

    private Mock<INextUpDisplay> _nextUpScene = default!;
    private Mock<IControl> _emptyQueueScene = default!;
    private Mock<ILabel> _bgMusicNowPlayingLabel = default!;
    private Mock<ILabel> _bgMusicPausedIndicator = default!;
    private Mock<IHBoxContainer> _bgMusicPlayingListing = default!;
    private Mock<ICdgRendererNode> _cdgRendererNode = default!;

    [Setup]
    public async Task Setup()
    {
        _fixture = new(TestScene.GetTree());

        _nextUpScene = new();
        _emptyQueueScene = new();
        _bgMusicNowPlayingLabel = new();
        _bgMusicPausedIndicator = new();
        _bgMusicPlayingListing = new();
        _cdgRendererNode = new();

        _scene = new DisplayScreen();
        _scene.FakeNodeTree(new()
        {
            [$"%{nameof(DisplayScreen.NextUpScene)}"] = _nextUpScene.Object,
            [$"%{nameof(DisplayScreen.EmptyQueueScene)}"] = _emptyQueueScene.Object,
            [$"%{nameof(DisplayScreen.BgMusicNowPlayingLabel)}"] = _bgMusicNowPlayingLabel.Object,
            [$"%{nameof(DisplayScreen.BgMusicPausedIndicator)}"] = _bgMusicPausedIndicator.Object,
            [$"%{nameof(DisplayScreen.BgMusicPlayingListing)}"] = _bgMusicPlayingListing.Object,
            [$"%{nameof(DisplayScreen.CdgRendererNode)}"] = _cdgRendererNode.Object,
        });

        await _fixture.AddToRoot(_scene);
    }

    [Cleanup]
    public async Task Cleanup() => await _fixture.Cleanup();

    [Test]
    public void UpdateBgMusicNowPlaying_SetsLabelText()
    {
        _scene.UpdateBgMusicNowPlaying("hwaaa?!");

        _bgMusicNowPlayingLabel.VerifySet(x => x.Text = "hwaaa?!");
    }
}
