using NUnit.Framework;
using Moq;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace ImmichFrame.Core.Tests.Logic.Pool;

[TestFixture]
public class MemoryAssetsPoolTests
{
    private Mock<ImmichApi> _mockImmichApi;
    private Mock<IAccountSettings> _mockAccountSettings;
    private Mock<IGeneralSettings> _mockGeneralSettings;
    private MemoryAssetsPool _memoryAssetsPool;

    [SetUp]
    public void Setup()
    {
        _mockImmichApi = new Mock<ImmichApi>(null, null); // Base constructor requires ILogger, IHttpClientFactory, IOptions, pass null
        _mockAccountSettings = new Mock<IAccountSettings>();
        _mockGeneralSettings = new Mock<IGeneralSettings>();

        _memoryAssetsPool = new MemoryAssetsPool(_mockImmichApi.Object, _mockAccountSettings.Object, _mockGeneralSettings.Object);
    }

    private List<AssetResponseDto> CreateSampleAssets(int count, bool withExif, int yearCreated, AssetTypeEnum assetType)
    {
        var assets = new List<AssetResponseDto>();
        for (int i = 0; i < count; i++)
        {
            var extension = assetType == AssetTypeEnum.IMAGE ? "jpg" : "mp4";
            var asset = new AssetResponseDto
            {
                Id = Guid.NewGuid().ToString(),
                OriginalPath = $"/path/to/image{i}.{extension}",
                Type = assetType,
                ExifInfo = withExif ? new ExifResponseDto { DateTimeOriginal = new DateTime(yearCreated, 1, 1) } : null,
                People = new List<PersonWithFacesResponseDto>()
            };
            assets.Add(asset);
        }
        return assets;
    }

    private List<MemoryResponseDto> CreateSampleImageMemories(int memoryCount, int assetsPerMemory, bool withExifInAssets, int memoryYear)
    {
        return CreateSampleMemories(memoryCount, assetsPerMemory, withExifInAssets, memoryYear, AssetTypeEnum.IMAGE);
    }

    private List<MemoryResponseDto> CreateSampleVideoMemories(int memoryCount, int assetsPerMemory, bool withExifInAssets, int memoryYear)
    {
        return CreateSampleMemories(memoryCount, assetsPerMemory, withExifInAssets, memoryYear, AssetTypeEnum.VIDEO);
    }
    private List<MemoryResponseDto> CreateSampleMemories(int memoryCount, int assetsPerMemory, bool withExifInAssets, int memoryYear, AssetTypeEnum assetType)
    {
        var memories = new List<MemoryResponseDto>();
        for (int i = 0; i < memoryCount; i++)
        {
            var memory = new MemoryResponseDto
            {
                Id = $"Memory {i}",
                Assets = CreateSampleAssets(assetsPerMemory, withExifInAssets, memoryYear, assetType),
                Data = new OnThisDayDto { Year = memoryYear }
            };
            memories.Add(memory);
        }
        return memories;
    }

    [Test]
    public async Task LoadAssets_CallsSearchMemoriesAsync()
    {
        // Arrange
        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryResponseDto>());

        // Act
        // Access protected method via reflection for testing, or make it internal/public if design allows
        // For now, we assume LoadAssets is implicitly called by a public method of CachingApiAssetsPool (e.g. GetAsset)
        // Let's simulate this by calling a method that would trigger LoadAssets if cache is empty.
        // Since LoadAssets is protected, we'll test its effects via GetAsset.
        // We need to ensure the cache is empty or expired for LoadAssets to be called.
        await _memoryAssetsPool.GetAssets(1, CancellationToken.None); // This should trigger LoadAssets

        // Assert
        _mockImmichApi.Verify(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task LoadAssets_FetchesAssetInfo_WhenExifInfoIsNull()
    {
        // Arrange
        var memoryYear = DateTime.Now.Year - 2;
        var memories = CreateSampleImageMemories(1, 1, false, memoryYear); // Asset without ExifInfo
        var assetId = memories[0].Assets.First().Id;

        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);
        _mockImmichApi.Setup(x => x.GetAssetInfoAsync(new Guid(assetId), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetResponseDto { Id = assetId, ExifInfo = new ExifResponseDto { DateTimeOriginal = new DateTime(memoryYear, 1, 1) }, People = new List<PersonWithFacesResponseDto>() });

        // Act
        var resultAsset = (await _memoryAssetsPool.GetAssets(1, CancellationToken.None)).First(); // Triggers LoadAssets

        // Assert
        _mockImmichApi.Verify(x => x.GetAssetInfoAsync(new Guid(assetId), null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(resultAsset.ExifInfo, Is.Not.Null);
        Assert.That(resultAsset.ExifInfo.Description, Is.EqualTo("2 years ago"));
    }

    [Test]
    public async Task LoadAssets_DoesNotFetchAssetInfo_WhenExifInfoIsPresent()
    {
        // Arrange
        var memoryYear = DateTime.Now.Year - 1;
        var memories = CreateSampleImageMemories(1, 1, true, memoryYear); // Asset with ExifInfo
        var assetId = memories[0].Assets.First().Id;

        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        // Act
        var resultAsset = (await _memoryAssetsPool.GetAssets(1, CancellationToken.None)).First(); // Triggers LoadAssets

        // Assert
        _mockImmichApi.Verify(x => x.GetAssetInfoAsync(It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(resultAsset.ExifInfo, Is.Not.Null);
        Assert.That(resultAsset.ExifInfo.Description, Is.EqualTo("1 year ago"));
    }

    [Test]
    public async Task LoadAssets_CorrectlyFormatsDescription_YearsAgo()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var testCases = new[]
        {
            new { year = currentYear - 1, expectedDesc = "1 year ago" },
            new { year = currentYear - 5, expectedDesc = "5 years ago" },
            new { year = currentYear, expectedDesc = "0 years ago" } // Or "This year" depending on desired logic, current is "0 years ago"
        };

        foreach (var tc in testCases)
        {
            var memories = CreateSampleImageMemories(1, 1, true, tc.year);
            memories[0].Assets.First().ExifInfo.DateTimeOriginal = new DateTime(tc.year, 1, 1); // Ensure Exif has the year

            _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(memories);

            _memoryAssetsPool = new MemoryAssetsPool(_mockImmichApi.Object, _mockAccountSettings.Object, _mockGeneralSettings.Object);


            // Act
            var resultAsset = (await _memoryAssetsPool.GetAssets(1, CancellationToken.None)).First(); // Triggers LoadAssets

            // Assert
            Assert.That(resultAsset.ExifInfo, Is.Not.Null);
            Assert.That(resultAsset.ExifInfo.Description, Is.EqualTo(tc.expectedDesc), $"Failed for year {tc.year}");
        }
    }

    [Test]
    public async Task LoadAssets_AggregatesAssetsFromMultipleMemories()
    {
        var imageMemoriesCount = 2;
        var videoMemoriesCount = 2;
        var assetsPerMemory = 2;

        // Arrange
        var memoryYear = DateTime.Now.Year - 3;
        var memories = CreateSampleImageMemories(imageMemoriesCount, assetsPerMemory, true, memoryYear); // 2 memories, 2 assets each
        memories.AddRange(CreateSampleVideoMemories(videoMemoriesCount, assetsPerMemory, true, memoryYear)); // 2 video memories, 2 assets each

        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories).Verifiable(Times.Once);

        // Act
        // We will rely on the fact that the factory in GetFromCacheAsync is called, and it returns the list.
        // The count can be indirectly verified if we could access the pool's internal list after LoadAssets.

        // Let's refine the test to ensure LoadAssets returns the correct number of assets.
        // We need a way to inspect the result of LoadAssets directly.
        // We can make LoadAssets internal and use InternalsVisibleTo, or use reflection.
        // Or, we can rely on the setup of GetFromCacheAsync to capture the factory's result.
        var loadedAssets = await _memoryAssetsPool.GetAssets(memories.Count * assetsPerMemory, CancellationToken.None); // Trigger load

        // Assert
        Assert.That(loadedAssets, Is.Not.Null);
        Assert.That(loadedAssets.All(x => x.Type == AssetTypeEnum.IMAGE), Is.True);
        Assert.That(loadedAssets.Count(), Is.EqualTo(4)); // 2 memories * 2 assets
        _mockImmichApi.VerifyAll();

    }

    [Test]
    public async Task LoadAssets_AggregatesAssetsFromMultipleMemories_WithVideo()
    {
        var imageMemoriesCount = 2;
        var videoMemoriesCount = 2;
        var assetsPerMemory = 2;

        // Arrange
        var memoryYear = DateTime.Now.Year - 3;
        var memories = CreateSampleImageMemories(imageMemoriesCount, assetsPerMemory, true, memoryYear); // 2 memories, 2 assets each
        memories.AddRange(CreateSampleVideoMemories(videoMemoriesCount, assetsPerMemory, true, memoryYear)); // 2 video memories, 2 assets each

        _mockAccountSettings.Setup(x => x.ShowVideos).Returns(true);
        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories).Verifiable(Times.Once);

        // Act
        // We will rely on the fact that the factory in GetFromCacheAsync is called, and it returns the list.
        // The count can be indirectly verified if we could access the pool's internal list after LoadAssets.

        // Let's refine the test to ensure LoadAssets returns the correct number of assets.
        // We need a way to inspect the result of LoadAssets directly.
        // We can make LoadAssets internal and use InternalsVisibleTo, or use reflection.
        // Or, we can rely on the setup of GetFromCacheAsync to capture the factory's result.
        var loadedAssets = await _memoryAssetsPool.GetAssets(memories.Count * assetsPerMemory, CancellationToken.None); // Trigger load

        // Assert
        Assert.That(loadedAssets, Is.Not.Null);
        Assert.That(loadedAssets.Count(), Is.EqualTo(8)); // 4 memories * 2 assets
        _mockImmichApi.VerifyAll();

    }

    [Test]
    public async Task LoadAssets_GroupsByYear_WhenGroupMemoriesIsTrue()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var memoriesYear1 = CreateSampleImageMemories(1, 2, true, currentYear - 1);
        var memoriesYear3 = CreateSampleImageMemories(1, 2, true, currentYear - 3);
        var memoriesYear2 = CreateSampleImageMemories(1, 2, true, currentYear - 2);

        var allMemories = new List<MemoryResponseDto>();
        allMemories.AddRange(memoriesYear3);
        allMemories.AddRange(memoriesYear1);
        allMemories.AddRange(memoriesYear2);

        _mockGeneralSettings.Setup(x => x.GroupMemories).Returns(true);
        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allMemories);

        // Act
        var result = (await _memoryAssetsPool.GetAssets(6, CancellationToken.None)).ToList();

        // Assert — sorted: year1 first, then year2, then year3
        Assert.That(result.Count, Is.EqualTo(6));
        Assert.That(result[0].ExifInfo.Description, Does.Contain("1"));
        Assert.That(result[1].ExifInfo.Description, Does.Contain("1"));
        Assert.That(result[2].ExifInfo.Description, Does.Contain("2"));
        Assert.That(result[3].ExifInfo.Description, Does.Contain("2"));
        Assert.That(result[4].ExifInfo.Description, Does.Contain("3"));
        Assert.That(result[5].ExifInfo.Description, Does.Contain("3"));
    }

    [Test]
    public async Task LoadAssets_UsesMemoryLabelFormat_WhenSet()
    {
        var memoryYear = DateTime.Now.Year - 2;
        var memories = CreateSampleImageMemories(1, 1, true, memoryYear);

        _mockGeneralSettings.Setup(x => x.MemoryLabelFormat).Returns("Vor {0} Jahren");
        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        var result = (await _memoryAssetsPool.GetAssets(1, CancellationToken.None)).First();

        Assert.That(result.ExifInfo.Description, Is.EqualTo("Vor 2 Jahren"));
    }

    [Test]
    public async Task LoadAssets_UsesSingularFormat_WhenOneYearAgo()
    {
        var memoryYear = DateTime.Now.Year - 1;
        var memories = CreateSampleImageMemories(1, 1, true, memoryYear);

        _mockGeneralSettings.Setup(x => x.MemoryLabelFormat).Returns("Vor {0} Jahren");
        _mockGeneralSettings.Setup(x => x.MemoryLabelFormatSingular).Returns("Vor {0} Jahr");
        _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        var result = (await _memoryAssetsPool.GetAssets(1, CancellationToken.None)).First();

        Assert.That(result.ExifInfo.Description, Is.EqualTo("Vor 1 Jahr"));
    }
}
