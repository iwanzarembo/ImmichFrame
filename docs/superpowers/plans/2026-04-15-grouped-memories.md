# Grouped Memories with Year Label — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add configurable Memory grouping by year and a persistent "Vor X Jahren" overlay to ImmichFrame, with a fallback to random library images when no Memories exist.

**Architecture:** Three new settings (`GroupMemories`, `MemoryLabelFormat`, `MemoryLabelFormatSingular`) are added to `IGeneralSettings` and propagated through `GeneralSettings`, `ServerSettingsV1`, `ClientSettingsDto`, and the V1 adapter. `CachingApiAssetsPool` gains a virtual `PreserveOrder` property that `MemoryAssetsPool` overrides to keep year-sorted order. `PooledImmichFrameLogic.BuildPool()` adds an `AllAssetsPool` fallback when only Memories are configured. The frontend gets a new `memory-label.svelte` component positioned near the clock.

**Tech Stack:** C# (.NET 8), NUnit + Moq (tests), Svelte 5 (runes/`$state`/`$derived`), Tailwind CSS, oazapfts (API codegen)

**Spec:** `docs/superpowers/specs/2026-04-15-grouped-memories-design.md`

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `ImmichFrame.Core/Interfaces/IServerSettings.cs` | Modify | Add 3 new properties to `IGeneralSettings` |
| `ImmichFrame.WebApi/Models/ServerSettings.cs` | Modify | Implement new properties in `GeneralSettings` |
| `ImmichFrame.WebApi/Helpers/Config/ServerSettingsV1.cs` | Modify | Add V1 properties + V1 adapter mappings |
| `ImmichFrame.WebApi/Models/ClientSettingsDto.cs` | Modify | Add DTO properties + mapping |
| `ImmichFrame.Core/Logic/Pool/CachingApiAssetsPool.cs` | Modify | Add `PreserveOrder` virtual property, conditional shuffle |
| `ImmichFrame.Core/Logic/Pool/MemoryAssetsPool.cs` | Modify | Accept `IGeneralSettings`, sort by year, format label, override `PreserveOrder` |
| `ImmichFrame.Core/Logic/PooledImmichFrameLogic.cs` | Modify | Add `AllAssetsPool` fallback when Memory-only and pool is empty |
| `ImmichFrame.Core.Tests/Logic/Pool/MemoryAssetsPoolTests.cs` | Modify | Add tests for grouping, label format, singular |
| `ImmichFrame.Core.Tests/Logic/Pool/CachingApiAssetsPoolTests.cs` | Modify | Add test for `PreserveOrder` |
| `immichFrame.Web/src/lib/components/elements/memory-label.svelte` | Create | New overlay component |
| `immichFrame.Web/src/lib/components/home-page/home-page.svelte` | Modify | Integrate memory-label |
| `immichFrame.Web/src/lib/components/elements/asset-info.svelte` | Modify | Suppress duplicate description |

---

### Task 1: Add new settings to IGeneralSettings interface

**Files:**
- Modify: `ImmichFrame.Core/Interfaces/IServerSettings.cs:33-71`

- [ ] **Step 1: Add the three new properties to IGeneralSettings**

In `ImmichFrame.Core/Interfaces/IServerSettings.cs`, add to the `IGeneralSettings` interface after `public string Language { get; }` (line 68):

```csharp
public bool GroupMemories { get; }
public string? MemoryLabelFormat { get; }
public string? MemoryLabelFormatSingular { get; }
```

- [ ] **Step 2: Verify the project still compiles (it won't yet — that's expected)**

Run: `dotnet build ImmichFrame.Core/ImmichFrame.Core.csproj`
Expected: Build succeeds (interface only, no implementations yet broken because nothing references Core alone). Actual implementations will fail in the WebApi project — that's fine, we fix them in the next tasks.

- [ ] **Step 3: Commit**

```bash
git add ImmichFrame.Core/Interfaces/IServerSettings.cs
git commit -m "feat: add GroupMemories, MemoryLabelFormat, MemoryLabelFormatSingular to IGeneralSettings"
```

---

### Task 2: Implement new settings in GeneralSettings and V1 adapter

**Files:**
- Modify: `ImmichFrame.WebApi/Models/ServerSettings.cs:38-77`
- Modify: `ImmichFrame.WebApi/Helpers/Config/ServerSettingsV1.cs:8-59` (V1 class) and `:101-140` (V1 adapter)

- [ ] **Step 1: Add properties to GeneralSettings class**

In `ImmichFrame.WebApi/Models/ServerSettings.cs`, add after `public string? AuthenticationSecret { get; set; }` (line 74):

```csharp
public bool GroupMemories { get; set; } = false;
public string? MemoryLabelFormat { get; set; }
public string? MemoryLabelFormatSingular { get; set; }
```

- [ ] **Step 2: Add properties to ServerSettingsV1**

In `ImmichFrame.WebApi/Helpers/Config/ServerSettingsV1.cs`, add after `public string Layout { get; set; } = "splitview";` (line 58):

```csharp
public bool GroupMemories { get; set; } = false;
public string? MemoryLabelFormat { get; set; }
public string? MemoryLabelFormatSingular { get; set; }
```

- [ ] **Step 3: Add mappings to GeneralSettingsV1Adapter**

In `ImmichFrame.WebApi/Helpers/Config/ServerSettingsV1.cs`, inside the `GeneralSettingsV1Adapter` class, add after `public string Language => _delegate.Language;` (line 137):

```csharp
public bool GroupMemories => _delegate.GroupMemories;
public string? MemoryLabelFormat => _delegate.MemoryLabelFormat;
public string? MemoryLabelFormatSingular => _delegate.MemoryLabelFormatSingular;
```

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build ImmichFrame.sln`
Expected: Build succeeds with no errors.

- [ ] **Step 5: Commit**

```bash
git add ImmichFrame.WebApi/Models/ServerSettings.cs ImmichFrame.WebApi/Helpers/Config/ServerSettingsV1.cs
git commit -m "feat: implement GroupMemories settings in GeneralSettings and V1 adapter"
```

---

### Task 3: Add new fields to ClientSettingsDto

**Files:**
- Modify: `ImmichFrame.WebApi/Models/ClientSettingsDto.cs:6-68`

- [ ] **Step 1: Add DTO properties**

In `ImmichFrame.WebApi/Models/ClientSettingsDto.cs`, add after `public string Language { get; set; }` (line 34):

```csharp
public bool GroupMemories { get; set; }
public string? MemoryLabelFormat { get; set; }
public string? MemoryLabelFormatSingular { get; set; }
```

- [ ] **Step 2: Add mappings in FromGeneralSettings**

In `ImmichFrame.WebApi/Models/ClientSettingsDto.cs`, add after `dto.Language = generalSettings.Language;` (line 66):

```csharp
dto.GroupMemories = generalSettings.GroupMemories;
dto.MemoryLabelFormat = generalSettings.MemoryLabelFormat;
dto.MemoryLabelFormatSingular = generalSettings.MemoryLabelFormatSingular;
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build ImmichFrame.sln`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add ImmichFrame.WebApi/Models/ClientSettingsDto.cs
git commit -m "feat: add GroupMemories fields to ClientSettingsDto"
```

---

### Task 4: Add PreserveOrder to CachingApiAssetsPool

**Files:**
- Modify: `ImmichFrame.Core/Logic/Pool/CachingApiAssetsPool.cs:1-29`
- Modify: `ImmichFrame.Core.Tests/Logic/Pool/CachingApiAssetsPoolTests.cs`

- [ ] **Step 1: Write the failing test for PreserveOrder**

In `ImmichFrame.Core.Tests/Logic/Pool/CachingApiAssetsPoolTests.cs`, add a new test class variant and test. First, add after the existing `TestableCachingApiAssetsPool` class (after line 31):

```csharp
private class OrderPreservingCachingApiAssetsPool : CachingApiAssetsPool
{
    public Func<Task<IEnumerable<AssetResponseDto>>> LoadAssetsFunc { get; set; }
    protected override bool PreserveOrder => true;

    public OrderPreservingCachingApiAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings)
        : base(apiCache, immichApi, accountSettings)
    {
    }

    protected override Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default)
    {
        return LoadAssetsFunc != null ? LoadAssetsFunc() : Task.FromResult(Enumerable.Empty<AssetResponseDto>());
    }
}
```

Then add this test method at the end of the test class:

```csharp
[Test]
public async Task GetAssets_PreservesOrder_WhenPreserveOrderIsTrue()
{
    // Arrange
    var orderedAssets = new List<AssetResponseDto>
    {
        new AssetResponseDto { Id = "A", Type = AssetTypeEnum.IMAGE, IsArchived = false, ExifInfo = new ExifResponseDto { DateTimeOriginal = DateTime.Now } },
        new AssetResponseDto { Id = "B", Type = AssetTypeEnum.IMAGE, IsArchived = false, ExifInfo = new ExifResponseDto { DateTimeOriginal = DateTime.Now } },
        new AssetResponseDto { Id = "C", Type = AssetTypeEnum.IMAGE, IsArchived = false, ExifInfo = new ExifResponseDto { DateTimeOriginal = DateTime.Now } },
    };

    var preservingPool = new OrderPreservingCachingApiAssetsPool(_mockApiCache.Object, _mockImmichApi.Object, _mockAccountSettings.Object);
    preservingPool.LoadAssetsFunc = () => Task.FromResult<IEnumerable<AssetResponseDto>>(orderedAssets);

    // Act
    var result = (await preservingPool.GetAssets(3)).ToList();

    // Assert — order must be preserved exactly
    Assert.That(result.Count, Is.EqualTo(3));
    Assert.That(result[0].Id, Is.EqualTo("A"));
    Assert.That(result[1].Id, Is.EqualTo("B"));
    Assert.That(result[2].Id, Is.EqualTo("C"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ImmichFrame.Core.Tests --filter "GetAssets_PreservesOrder_WhenPreserveOrderIsTrue" -v n`
Expected: FAIL — `PreserveOrder` property does not exist yet.

- [ ] **Step 3: Implement PreserveOrder in CachingApiAssetsPool**

In `ImmichFrame.Core/Logic/Pool/CachingApiAssetsPool.cs`, replace the entire `GetAssets` method and add the virtual property:

```csharp
public abstract class CachingApiAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings) : IAssetPool
{
    private readonly Random _random = new();

    protected virtual bool PreserveOrder => false;

    public async Task<long> GetAssetCount(CancellationToken ct = default)
    {
        return (await AllAssets(ct)).Count();
    }

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        var assets = await AllAssets(ct);
        if (!PreserveOrder)
        {
            assets = assets.OrderBy(_ => _random.Next());
        }
        return assets.Take(requested);
    }

    private async Task<IEnumerable<AssetResponseDto>> AllAssets(CancellationToken ct = default)
    {
        var excludedAlbumAssets = await apiCache.GetOrAddAsync($"{GetType().FullName}_ExcludedAlbums", () => AssetHelper.GetExcludedAlbumAssets(immichApi, accountSettings));

        return await apiCache.GetOrAddAsync(GetType().FullName!, () => LoadAssets().ApplyAccountFilters(accountSettings, excludedAlbumAssets));
    }

    protected abstract Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ImmichFrame.Core.Tests --filter "CachingApiAssetsPoolTests" -v n`
Expected: All tests PASS, including the new `GetAssets_PreservesOrder_WhenPreserveOrderIsTrue`.

- [ ] **Step 5: Commit**

```bash
git add ImmichFrame.Core/Logic/Pool/CachingApiAssetsPool.cs ImmichFrame.Core.Tests/Logic/Pool/CachingApiAssetsPoolTests.cs
git commit -m "feat: add PreserveOrder virtual property to CachingApiAssetsPool"
```

---

### Task 5: Update MemoryAssetsPool for grouping, sorting, and label formatting

**Files:**
- Modify: `ImmichFrame.Core/Logic/Pool/MemoryAssetsPool.cs:1-54`
- Modify: `ImmichFrame.Core.Tests/Logic/Pool/MemoryAssetsPoolTests.cs`

- [ ] **Step 1: Write failing tests for grouped memories**

In `ImmichFrame.Core.Tests/Logic/Pool/MemoryAssetsPoolTests.cs`, update the `Setup` method and add new tests.

First, add a `_mockGeneralSettings` field after line 19:

```csharp
private Mock<IGeneralSettings> _mockGeneralSettings;
```

Update `Setup()` to initialize it and pass it to the constructor:

```csharp
[SetUp]
public void Setup()
{
    _mockImmichApi = new Mock<ImmichApi>(null, null);
    _mockAccountSettings = new Mock<IAccountSettings>();
    _mockGeneralSettings = new Mock<IGeneralSettings>();

    _memoryAssetsPool = new MemoryAssetsPool(_mockImmichApi.Object, _mockAccountSettings.Object, _mockGeneralSettings.Object);
}
```

Also update the existing test at line 155 (`CorrectlyFormatsDescription_YearsAgo`) where a new `MemoryAssetsPool` is created inside the loop:

```csharp
_memoryAssetsPool = new MemoryAssetsPool(_mockImmichApi.Object, _mockAccountSettings.Object, _mockGeneralSettings.Object);
```

Then add these new test methods at the end of the class:

```csharp
[Test]
public async Task LoadAssets_GroupsByYear_WhenGroupMemoriesIsTrue()
{
    // Arrange
    var currentYear = DateTime.Now.Year;
    var memoriesYear1 = CreateSampleImageMemories(1, 2, true, currentYear - 1);
    var memoriesYear3 = CreateSampleImageMemories(1, 2, true, currentYear - 3);
    var memoriesYear2 = CreateSampleImageMemories(1, 2, true, currentYear - 2);

    // Combine in non-sorted order: year3, year1, year2
    var allMemories = new List<MemoryResponseDto>();
    allMemories.AddRange(memoriesYear3);
    allMemories.AddRange(memoriesYear1);
    allMemories.AddRange(memoriesYear2);

    _mockGeneralSettings.Setup(x => x.GroupMemories).Returns(true);
    _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(allMemories);

    // Act
    var result = (await _memoryAssetsPool.GetAssets(6, CancellationToken.None)).ToList();

    // Assert — should be sorted: year1 (newest) first, then year2, then year3
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
    // Arrange
    var memoryYear = DateTime.Now.Year - 2;
    var memories = CreateSampleImageMemories(1, 1, true, memoryYear);

    _mockGeneralSettings.Setup(x => x.MemoryLabelFormat).Returns("Vor {0} Jahren");
    _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(memories);

    // Act
    var result = (await _memoryAssetsPool.GetAssets(1, CancellationToken.None)).First();

    // Assert
    Assert.That(result.ExifInfo.Description, Is.EqualTo("Vor 2 Jahren"));
}

[Test]
public async Task LoadAssets_UsesSingularFormat_WhenOneYearAgo()
{
    // Arrange
    var memoryYear = DateTime.Now.Year - 1;
    var memories = CreateSampleImageMemories(1, 1, true, memoryYear);

    _mockGeneralSettings.Setup(x => x.MemoryLabelFormat).Returns("Vor {0} Jahren");
    _mockGeneralSettings.Setup(x => x.MemoryLabelFormatSingular).Returns("Vor {0} Jahr");
    _mockImmichApi.Setup(x => x.SearchMemoriesAsync(It.IsAny<DateTimeOffset>(), null, null, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(memories);

    // Act
    var result = (await _memoryAssetsPool.GetAssets(1, CancellationToken.None)).First();

    // Assert
    Assert.That(result.ExifInfo.Description, Is.EqualTo("Vor 1 Jahr"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ImmichFrame.Core.Tests --filter "MemoryAssetsPoolTests" -v n`
Expected: New tests FAIL because `MemoryAssetsPool` constructor doesn't accept `IGeneralSettings` yet, existing tests also fail because of the changed constructor.

- [ ] **Step 3: Implement the updated MemoryAssetsPool**

Replace the entire content of `ImmichFrame.Core/Logic/Pool/MemoryAssetsPool.cs`:

```csharp
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ImmichFrame.Core.Logic.Pool;

public class MemoryAssetsPool(ImmichApi immichApi, IAccountSettings accountSettings, IGeneralSettings generalSettings) : CachingApiAssetsPool(new DailyApiCache(), immichApi, accountSettings)
{
    protected override bool PreserveOrder => generalSettings.GroupMemories;

    protected override async Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default)
    {
        var searchDate = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc), TimeSpan.Zero);
        var memories = await immichApi.SearchMemoriesAsync(searchDate, null, null, null, ct);

        var memoryAssets = new List<(int yearsAgo, AssetResponseDto asset)>();
        foreach (var memory in memories)
        {
            var assets = memory.Assets.ToList();
            var yearsAgo = searchDate.Year - memory.Data.Year;

            if (!accountSettings.ShowVideos)
            {
                assets = assets.Where(a => a.Type == AssetTypeEnum.IMAGE).ToList();
            }

            foreach (var asset in assets)
            {
                if (asset.ExifInfo == null)
                {
                    var assetInfo = await immichApi.GetAssetInfoAsync(new Guid(asset.Id), null, ct);
                    asset.ExifInfo = assetInfo.ExifInfo;
                    asset.People = assetInfo.People;
                }

                asset.ExifInfo.Description = FormatMemoryLabel(yearsAgo);
                memoryAssets.Add((yearsAgo, asset));
            }
        }

        if (generalSettings.GroupMemories)
        {
            return memoryAssets.OrderBy(m => m.yearsAgo).Select(m => m.asset);
        }

        return memoryAssets.Select(m => m.asset);
    }

    private string FormatMemoryLabel(int yearsAgo)
    {
        if (!string.IsNullOrEmpty(generalSettings.MemoryLabelFormat))
        {
            if (yearsAgo == 1 && !string.IsNullOrEmpty(generalSettings.MemoryLabelFormatSingular))
            {
                return string.Format(generalSettings.MemoryLabelFormatSingular, yearsAgo);
            }
            return string.Format(generalSettings.MemoryLabelFormat, yearsAgo);
        }

        return $"{yearsAgo} {(yearsAgo == 1 ? "year" : "years")} ago";
    }
}

class DailyApiCache : ApiCache
{
    public DailyApiCache() : base(() => new MemoryCacheEntryOptions
    {
        AbsoluteExpiration = DateTimeOffset.Now.Date.AddDays(1)
    }
    )
    {
    }
}
```

- [ ] **Step 4: Run all MemoryAssetsPool tests**

Run: `dotnet test ImmichFrame.Core.Tests --filter "MemoryAssetsPoolTests" -v n`
Expected: All tests PASS (old and new).

- [ ] **Step 5: Run all Core tests to check for regressions**

Run: `dotnet test ImmichFrame.Core.Tests -v n`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add ImmichFrame.Core/Logic/Pool/MemoryAssetsPool.cs ImmichFrame.Core.Tests/Logic/Pool/MemoryAssetsPoolTests.cs
git commit -m "feat: add year grouping, sorting, and label formatting to MemoryAssetsPool"
```

---

### Task 6: Update PooledImmichFrameLogic to pass IGeneralSettings and add fallback

**Files:**
- Modify: `ImmichFrame.Core/Logic/PooledImmichFrameLogic.cs:37-66`

- [ ] **Step 1: Update BuildPool to pass generalSettings to MemoryAssetsPool**

In `ImmichFrame.Core/Logic/PooledImmichFrameLogic.cs`, change the `MemoryAssetsPool` construction in `BuildPool()`. Replace line 53:

```csharp
// Old:
pools.Add(new MemoryAssetsPool(_immichApi, accountSettings));
// New:
pools.Add(new MemoryAssetsPool(_immichApi, accountSettings, _generalSettings));
```

- [ ] **Step 2: Add AllAssetsPool fallback for memory-only configuration**

In `BuildPool()`, after all pools are built, add a fallback. Replace the return statement at the end of `BuildPool()` (currently `return new MultiAssetPool(pools);`):

```csharp
if (pools.Count == 0)
{
    return new AllAssetsPool(_apiCache, _immichApi, accountSettings);
}

if (pools.Count == 1 && accountSettings.ShowMemories && !accountSettings.ShowFavorites && !hasAlbums && !hasPeople && !hasTags)
{
    // Memory-only config: wrap in a MultiAssetPool with AllAssetsPool as fallback
    // so if no memories exist today, random images are shown
    pools.Add(new AllAssetsPool(_apiCache, _immichApi, accountSettings));
}

return new MultiAssetPool(pools);
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build ImmichFrame.sln`
Expected: Build succeeds.

- [ ] **Step 4: Run all tests**

Run: `dotnet test ImmichFrame.sln -v n`
Expected: All tests PASS.

- [ ] **Step 5: Commit**

```bash
git add ImmichFrame.Core/Logic/PooledImmichFrameLogic.cs
git commit -m "feat: pass IGeneralSettings to MemoryAssetsPool, add AllAssetsPool fallback"
```

---

### Task 7: Regenerate frontend API types

**Files:**
- Modify: `immichFrame.Web/src/lib/immichFrameApi.ts` (auto-generated)

- [ ] **Step 1: Start the dev server temporarily to export swagger.json**

Run: `cd C:\SAPDevelop\ImmichFrame && dotnet run --project ./ImmichFrame.WebApi &`

Wait for the server to start (check for "Now listening on" in output), then:

Run: `curl http://localhost:5217/swagger/v1/swagger.json -o ./openApi/swagger.json`

Stop the server.

- [ ] **Step 2: Regenerate the TypeScript API client**

Run: `cd C:\SAPDevelop\ImmichFrame && npm --prefix immichFrame.Web run api`

Expected: `immichFrame.Web/src/lib/immichFrameApi.ts` is updated with the new `ClientSettingsDto` fields:
```typescript
groupMemories?: boolean;
memoryLabelFormat?: string | null;
memoryLabelFormatSingular?: string | null;
```

- [ ] **Step 3: Verify the generated file contains the new fields**

Run: `grep -n "groupMemories\|memoryLabelFormat" immichFrame.Web/src/lib/immichFrameApi.ts`

Expected: The new fields appear in the `ClientSettingsDto` type.

- [ ] **Step 4: Commit**

```bash
git add openApi/swagger.json immichFrame.Web/src/lib/immichFrameApi.ts
git commit -m "chore: regenerate API types with GroupMemories settings"
```

---

### Task 8: Create memory-label.svelte component

**Files:**
- Create: `immichFrame.Web/src/lib/components/elements/memory-label.svelte`

- [ ] **Step 1: Create the memory-label component**

Create `immichFrame.Web/src/lib/components/elements/memory-label.svelte`:

```svelte
<script lang="ts">
	import { configStore } from '$lib/stores/config.store';
	import type { AssetResponseDto } from '$lib/immichFrameApi';

	interface Props {
		assets: AssetResponseDto[];
	}

	let { assets }: Props = $props();

	const memoryDescription = $derived(() => {
		if (!$configStore.groupMemories || !$configStore.memoryLabelFormat) {
			return null;
		}

		if (assets.length === 0) {
			return null;
		}

		const desc = assets[0]?.exifInfo?.description;
		if (!desc) {
			return null;
		}

		// Check if the description matches the memory label pattern
		// The backend sets description using the MemoryLabelFormat, so we check
		// if it looks like a formatted memory label (contains a digit)
		if (/\d/.test(desc)) {
			return desc;
		}

		return null;
	});
</script>

{#if memoryDescription()}
	<p
		id="memorylabel"
		class="text-xl sm:text-xl md:text-2xl lg:text-4xl font-semibold text-shadow-lg text-primary"
	>
		{memoryDescription()}
	</p>
{/if}
```

- [ ] **Step 2: Verify the frontend builds**

Run: `cd C:\SAPDevelop\ImmichFrame\immichFrame.Web && npm run build`

Expected: Build succeeds with no errors.

- [ ] **Step 3: Commit**

```bash
git add immichFrame.Web/src/lib/components/elements/memory-label.svelte
git commit -m "feat: add memory-label.svelte component"
```

---

### Task 9: Integrate memory-label into home-page and clock area

**Files:**
- Modify: `immichFrame.Web/src/lib/components/home-page/home-page.svelte:1-542`
- Modify: `immichFrame.Web/src/lib/components/elements/clock.svelte:1-103`

- [ ] **Step 1: Import MemoryLabel in home-page.svelte**

In `immichFrame.Web/src/lib/components/home-page/home-page.svelte`, add the import after the `Clock` import (line 12):

```typescript
import MemoryLabel from '../elements/memory-label.svelte';
```

- [ ] **Step 2: Add MemoryLabel next to Clock in the template**

In the template section, replace the `{#if $configStore.showClock}` block (lines 485-487):

```svelte
{#if $configStore.showClock}
	<Clock memoryAssets={displayingAssets} />
{/if}
```

- [ ] **Step 3: Update clock.svelte to accept and render the memory label**

In `immichFrame.Web/src/lib/components/elements/clock.svelte`, add the import and prop. Add after the existing imports (after line 7):

```typescript
import MemoryLabel from './memory-label.svelte';
import type { AssetResponseDto } from '$lib/immichFrameApi';
```

Add to the script section, after line 9 (`api.init();`):

```typescript
interface Props {
	memoryAssets?: AssetResponseDto[];
}

let { memoryAssets = [] }: Props = $props();
```

In the template, add the `<MemoryLabel>` inside the `#clock` div, right after the opening `<div id="clock" ...>` tag and before `<p id="clockdate"` (line 69):

```svelte
<MemoryLabel assets={memoryAssets} />
```

- [ ] **Step 4: Verify the frontend builds**

Run: `cd C:\SAPDevelop\ImmichFrame\immichFrame.Web && npm run build`

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add immichFrame.Web/src/lib/components/home-page/home-page.svelte immichFrame.Web/src/lib/components/elements/clock.svelte
git commit -m "feat: integrate memory-label into clock area"
```

---

### Task 10: Suppress duplicate description in asset-info

**Files:**
- Modify: `immichFrame.Web/src/lib/components/elements/asset-info.svelte:1-137`

- [ ] **Step 1: Add memory-label awareness to asset-info**

In `immichFrame.Web/src/lib/components/elements/asset-info.svelte`, add an import and derived check. After the existing imports (line 7):

```typescript
import { configStore } from '$lib/stores/config.store';
```

Note: `configStore` is already imported on line 5. So no new import needed — just add the derived value. After `let availableTags = ...` (line 70):

```typescript
let isMemoryLabel = $derived(
	$configStore.groupMemories &&
	$configStore.memoryLabelFormat &&
	desc &&
	/\d/.test(desc)
);
```

- [ ] **Step 2: Suppress description when memory label is active**

Replace the description `{#if}` block (lines 87-91):

```svelte
{#if showImageDesc && desc && !isMemoryLabel}
	<p id="imagedescription" class="info-item">
		<Icon path={mdiText} class="info-icon" />
		<span class="info-text" class:short-text={split}>{desc}</span>
	</p>
{/if}
```

- [ ] **Step 3: Verify the frontend builds**

Run: `cd C:\SAPDevelop\ImmichFrame\immichFrame.Web && npm run build`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add immichFrame.Web/src/lib/components/elements/asset-info.svelte
git commit -m "feat: suppress duplicate memory description in asset-info"
```

---

### Task 11: Full build and test verification

**Files:** None (verification only)

- [ ] **Step 1: Run all backend tests**

Run: `dotnet test ImmichFrame.sln -v n`

Expected: All tests PASS.

- [ ] **Step 2: Build the full frontend**

Run: `cd C:\SAPDevelop\ImmichFrame\immichFrame.Web && npm run build`

Expected: Build succeeds with no errors or warnings.

- [ ] **Step 3: Build the Docker image**

Run: `cd C:\SAPDevelop\ImmichFrame && docker buildx build --platform linux/amd64 . --target final -t immichframe-test:grouped-memories --build-arg VERSION=1.0.33.1`

Expected: Docker image builds successfully.

- [ ] **Step 4: Commit any remaining changes**

If any fixups were needed, commit them:

```bash
git add -A
git commit -m "fix: address build issues from grouped-memories feature"
```
