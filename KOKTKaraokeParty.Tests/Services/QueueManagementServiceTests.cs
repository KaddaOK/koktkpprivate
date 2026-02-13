using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using KOKTKaraokeParty.Services;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KOKTKaraokeParty.Tests.Services;

public class QueueManagementServiceTests(Node testScene) : TestClass(testScene)
{
    private Fixture _fixture = default!;
    private Mock<IFileWrapper> _mockFileWrapper = default!;
    private Mock<IYtDlpProviderNode> _mockYtDlpProvider = default!;
    private QueueManagementService _service = default!;

    [Setup]
    public async Task Setup()
    {
        _fixture = new(TestScene.GetTree());
        _mockFileWrapper = new Mock<IFileWrapper>();
        _mockYtDlpProvider = new Mock<IYtDlpProviderNode>();
        _service = new QueueManagementService();
        
        await _fixture.AddToRoot(_service);
    }

    [Cleanup]
    public async Task Cleanup() => await _fixture.Cleanup();

    private QueueItem CreateTestItem(string performanceLink, string singerName = "Test Singer")
    {
        return new QueueItem
        {
            PerformanceLink = performanceLink,
            SingerName = singerName,
            SongName = "Test Song",
            ArtistName = "Test Artist",
            CreatorName = "Test Creator",
            ItemType = ItemType.KarafunWeb
        };
    }

    [Test]
    public void Initialize_CreatesEmptyQueue_WhenNoSavedQueueExists()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);

        // Act
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        // Assert
        _service.QueueCount.ShouldBe(0);
        _service.NowPlaying.ShouldBeNull();
    }

    [Test]
    public void Initialize_LoadsQueueFromDisk_AndFiresQueueLoadedEvent()
    {
        // Arrange
        var testItems = new[]
        {
            CreateTestItem("http://test1.com", "Singer 1"),
            CreateTestItem("http://test2.com", "Singer 2"),
            CreateTestItem("http://test3.com", "Singer 3")
        };
        var queueJson = JsonConvert.SerializeObject(testItems);
        
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
        _mockFileWrapper.Setup(f => f.ReadAllText(It.IsAny<string>())).Returns(queueJson);

        bool queueLoadedFired = false;
        _service.QueueLoaded += () => queueLoadedFired = true;

        // Act
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        // Assert
        _service.QueueCount.ShouldBe(3);
        queueLoadedFired.ShouldBeTrue();
        
        // Verify all items are accessible
        var loadedItems = _service.GetQueueItems().ToList();
        loadedItems.Count.ShouldBe(3);
        loadedItems[0].SingerName.ShouldBe("Singer 1");
        loadedItems[1].SingerName.ShouldBe("Singer 2");
        loadedItems[2].SingerName.ShouldBe("Singer 3");
    }

    [Test]
    public void AddToQueue_AddsItem_AndFiresItemAddedEvent()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        QueueItem? addedItem = null;
        _service.ItemAdded += (item) => addedItem = item;

        var testItem = CreateTestItem("http://test.com");

        // Act
        _service.AddToQueue(testItem);

        // Assert
        _service.QueueCount.ShouldBe(1);
        addedItem.ShouldNotBeNull();
        addedItem!.PerformanceLink.ShouldBe(testItem.PerformanceLink);
    }

    [Test]
    public void RemoveFromQueue_RemovesItem_AndFiresItemRemovedEvent()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com");
        var item2 = CreateTestItem("http://test2.com");
        var item3 = CreateTestItem("http://test3.com");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.AddToQueue(item3);

        QueueItem? removedItem = null;
        _service.ItemRemoved += (item) => removedItem = item;

        // Act
        _service.RemoveFromQueue(item2);

        // Assert
        _service.QueueCount.ShouldBe(2);
        removedItem.ShouldNotBeNull();
        removedItem!.PerformanceLink.ShouldBe(item2.PerformanceLink);
        
        var remainingItems = _service.GetQueueItems().ToList();
        remainingItems.Count.ShouldBe(2);
        remainingItems.ShouldNotContain(item2);
    }

    [Test]
    public void ReorderQueue_MovesItem_AndFiresQueueReorderedEvent()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com", "Singer 1");
        var item2 = CreateTestItem("http://test2.com", "Singer 2");
        var item3 = CreateTestItem("http://test3.com", "Singer 3");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.AddToQueue(item3);

        bool queueReorderedFired = false;
        _service.QueueReordered += () => queueReorderedFired = true;

        // Act - Move item3 before item2 (dropSection = 0 means "before")
        _service.ReorderQueue(item3, item2, 0);

        // Assert
        queueReorderedFired.ShouldBeTrue();
        
        var reorderedItems = _service.GetQueueItems().ToList();
        reorderedItems.Count.ShouldBe(3);
        reorderedItems[0].SingerName.ShouldBe("Singer 1");
        reorderedItems[1].SingerName.ShouldBe("Singer 3"); // item3 moved before item2
        reorderedItems[2].SingerName.ShouldBe("Singer 2");
    }

    [Test]
    public void ReorderQueue_MovesItemAfter_WhenDropSectionIs1()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com", "Singer 1");
        var item2 = CreateTestItem("http://test2.com", "Singer 2");
        var item3 = CreateTestItem("http://test3.com", "Singer 3");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.AddToQueue(item3);

        // Act - Move item1 after item3 (dropSection = 1 means "after")
        _service.ReorderQueue(item1, item3, 1);

        // Assert
        var reorderedItems = _service.GetQueueItems().ToList();
        reorderedItems.Count.ShouldBe(3);
        reorderedItems[0].SingerName.ShouldBe("Singer 2");
        reorderedItems[1].SingerName.ShouldBe("Singer 3");
        reorderedItems[2].SingerName.ShouldBe("Singer 1"); // item1 moved after item3
    }

    [Test]
    public void GetNextInQueue_ReturnsFirstItem_AndUpdatesNowPlaying()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com");
        var item2 = CreateTestItem("http://test2.com");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);

        QueueItem? nowPlayingChangedItem = null;
        _service.NowPlayingChanged += (item) => nowPlayingChangedItem = item;

        // Act
        var nextItem = _service.GetNextInQueue();

        // Assert
        nextItem.ShouldNotBeNull();
        nextItem!.PerformanceLink.ShouldBe(item1.PerformanceLink);
        _service.QueueCount.ShouldBe(1); // One item should remain in queue
        _service.NowPlaying.ShouldBe(nextItem);
        nowPlayingChangedItem.ShouldNotBeNull();
    }

    [Test]
    public void GetNextInQueue_ReturnsNull_WhenQueueIsEmpty()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        // Act
        var nextItem = _service.GetNextInQueue();

        // Assert
        nextItem.ShouldBeNull();
    }

    [Test]
    public void FinishedPlaying_ClearsNowPlaying_AndFiresEvent()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item = CreateTestItem("http://test.com");
        _service.AddToQueue(item);
        _service.GetNextInQueue(); // Sets NowPlaying

        QueueItem? nowPlayingChangedItem = item; // Initialize to non-null
        _service.NowPlayingChanged += (i) => nowPlayingChangedItem = i;

        // Act
        _service.FinishedPlaying(item);

        // Assert
        _service.NowPlaying.ShouldBeNull();
        nowPlayingChangedItem.ShouldBeNull(); // Should be null after finishing
    }

    [Test]
    public void GetQueueItems_ReturnsAllItemsInQueue()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com", "Singer 1");
        var item2 = CreateTestItem("http://test2.com", "Singer 2");
        var item3 = CreateTestItem("http://test3.com", "Singer 3");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.AddToQueue(item3);

        // Act
        var items = _service.GetQueueItems().ToList();

        // Assert
        items.Count.ShouldBe(3);
        items[0].SingerName.ShouldBe("Singer 1");
        items[1].SingerName.ShouldBe("Singer 2");
        items[2].SingerName.ShouldBe("Singer 3");
    }

    [Test]
    public void GetQueueItems_DoesNotIncludeNowPlayingItem()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com");
        var item2 = CreateTestItem("http://test2.com");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.GetNextInQueue(); // Moves item1 to NowPlaying

        // Act
        var queueItems = _service.GetQueueItems().ToList();

        // Assert
        queueItems.Count.ShouldBe(1);
        queueItems[0].PerformanceLink.ShouldBe(item2.PerformanceLink);
        _service.NowPlaying.ShouldBe(item1);
    }

    [Test]
    public void PauseAndResume_TogglesState_AndFiresEvents()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var pausedStates = new List<bool>();
        _service.PausedStateChanged += (isPaused) => pausedStates.Add(isPaused);

        // Act & Assert
        _service.IsPaused.ShouldBeFalse();

        _service.Pause();
        _service.IsPaused.ShouldBeTrue();
        pausedStates.Count.ShouldBe(1);
        pausedStates[0].ShouldBeTrue();

        _service.Resume();
        _service.IsPaused.ShouldBeFalse();
        pausedStates.Count.ShouldBe(2);
        pausedStates[1].ShouldBeFalse();
    }

    #region ClearQueue Tests

    [Test]
    public void ClearQueue_RemovesAllItems_AndFiresQueueLoadedEvent()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com");
        var item2 = CreateTestItem("http://test2.com");
        var item3 = CreateTestItem("http://test3.com");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.AddToQueue(item3);

        bool queueLoadedFired = false;
        _service.QueueLoaded += () => queueLoadedFired = true;

        // Act
        _service.ClearQueue();

        // Assert
        _service.QueueCount.ShouldBe(0);
        _service.NowPlaying.ShouldBeNull();
        queueLoadedFired.ShouldBeTrue();
    }

    [Test]
    public void ClearQueue_ClearsNowPlaying_WhenItemIsPlaying()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com");
        var item2 = CreateTestItem("http://test2.com");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.GetNextInQueue(); // Sets item1 as NowPlaying

        _service.NowPlaying.ShouldNotBeNull();

        // Act
        _service.ClearQueue();

        // Assert
        _service.QueueCount.ShouldBe(0);
        _service.NowPlaying.ShouldBeNull();
    }

    [Test]
    public void ClearQueue_WorksOnEmptyQueue()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        bool queueLoadedFired = false;
        _service.QueueLoaded += () => queueLoadedFired = true;

        // Act
        _service.ClearQueue();

        // Assert
        _service.QueueCount.ShouldBe(0);
        queueLoadedFired.ShouldBeTrue();
    }

    #endregion

    #region RemoveFirstItem Tests

    [Test]
    public void RemoveFirstItem_RemovesAndReturnsFirstItem()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com", "Singer 1");
        var item2 = CreateTestItem("http://test2.com", "Singer 2");
        var item3 = CreateTestItem("http://test3.com", "Singer 3");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.AddToQueue(item3);

        // Act
        var removedItem = _service.RemoveFirstItem();

        // Assert
        removedItem.ShouldNotBeNull();
        removedItem!.SingerName.ShouldBe("Singer 1");
        _service.QueueCount.ShouldBe(2);
        
        var remainingItems = _service.GetQueueItems().ToList();
        remainingItems[0].SingerName.ShouldBe("Singer 2");
        remainingItems[1].SingerName.ShouldBe("Singer 3");
    }

    [Test]
    public void RemoveFirstItem_FiresItemRemovedEvent()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com", "Singer 1");
        var item2 = CreateTestItem("http://test2.com", "Singer 2");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);

        QueueItem? removedItem = null;
        _service.ItemRemoved += (item) => removedItem = item;

        // Act
        _service.RemoveFirstItem();

        // Assert
        removedItem.ShouldNotBeNull();
        removedItem!.SingerName.ShouldBe("Singer 1");
    }

    [Test]
    public void RemoveFirstItem_ReturnsNull_WhenQueueIsEmpty()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        // Act
        var removedItem = _service.RemoveFirstItem();

        // Assert
        removedItem.ShouldBeNull();
    }

    [Test]
    public void RemoveFirstItem_DoesNotAffectNowPlaying()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com", "Singer 1");
        var item2 = CreateTestItem("http://test2.com", "Singer 2");
        var item3 = CreateTestItem("http://test3.com", "Singer 3");
        
        _service.AddToQueue(item1);
        _service.AddToQueue(item2);
        _service.AddToQueue(item3);
        
        // Set item1 as NowPlaying (removes it from queue)
        _service.GetNextInQueue();
        
        // Now queue has [item2, item3] and NowPlaying is item1

        // Act
        var removedItem = _service.RemoveFirstItem();

        // Assert
        removedItem.ShouldNotBeNull();
        removedItem!.SingerName.ShouldBe("Singer 2"); // First in queue, not NowPlaying
        _service.NowPlaying.ShouldNotBeNull();
        _service.NowPlaying!.SingerName.ShouldBe("Singer 1"); // Still playing
        _service.QueueCount.ShouldBe(1); // Only item3 remains
    }

    [Test]
    public void RemoveFirstItem_LeavesQueueEmpty_WhenOnlyOneItem()
    {
        // Arrange
        _mockFileWrapper.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);
        _service.Initialize(_mockFileWrapper.Object, _mockYtDlpProvider.Object);

        var item1 = CreateTestItem("http://test1.com", "Singer 1");
        _service.AddToQueue(item1);

        // Act
        var removedItem = _service.RemoveFirstItem();

        // Assert
        removedItem.ShouldNotBeNull();
        removedItem!.SingerName.ShouldBe("Singer 1");
        _service.QueueCount.ShouldBe(0);
    }

    #endregion
}
