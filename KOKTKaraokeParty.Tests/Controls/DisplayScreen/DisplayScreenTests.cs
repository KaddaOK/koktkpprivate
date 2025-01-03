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

  [Setup]
  public async Task Setup()
  {
    _fixture = new(TestScene.GetTree());

    _nextUpScene = new();
    _emptyQueueScene = new();
    _bgMusicNowPlayingLabel = new();
    _bgMusicPausedIndicator = new();

    _scene = new DisplayScreen();
    _scene.FakeNodeTree(new()
    {
      ["%NextUpScene"] = _nextUpScene.Object,
      ["%EmptyQueueScene"] = _emptyQueueScene.Object,
      ["%BgMusicNowPlayingLabel"] = _bgMusicNowPlayingLabel.Object,
      ["%BgMusicPausedIndicator"] = _bgMusicPausedIndicator.Object,
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
