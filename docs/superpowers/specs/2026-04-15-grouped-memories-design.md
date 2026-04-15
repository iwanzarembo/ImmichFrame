# Grouped Memories with Year Label

## Summary

Add configurable Memory grouping and a year-label overlay to ImmichFrame. When enabled, Memories are displayed as consecutive blocks sorted by year (newest first) instead of randomly mixed with other assets. A persistent overlay near the clock shows "Vor X Jahren" (or a custom format) while Memory images are on screen.

Both features are off by default and fully backwards-compatible, making this suitable for an upstream PR.

## New Configuration Settings

### Account-level (`IAccountSettings`)

No changes.

### General-level (`IGeneralSettings` / env vars / `ClientSettingsDto`)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `GroupMemories` | `bool` | `false` | When `true`, Memory assets are sorted by year (newest first) and delivered in order instead of shuffled. |
| `MemoryLabelFormat` | `string?` | `null` | Format string for the year overlay (plural), e.g. `"Vor {0} Jahren"`. `{0}` is replaced with the number of years. When `null` or empty, no Memory label is shown. Singular/plural handling: two format fields — `MemoryLabelFormat` for plural (e.g. `"Vor {0} Jahren"`) and `MemoryLabelFormatSingular` for count=1 (e.g. `"Vor {0} Jahr"`). If singular is not set, the plural format is used for all counts. |
| `MemoryLabelFormatSingular` | `string?` | `null` | Singular variant of `MemoryLabelFormat`, used when `yearsAgo == 1`. E.g. `"Vor {0} Jahr"`. |

Docker env var mapping:
- `GroupMemories=true`
- `MemoryLabelFormat=Vor {0} Jahren`
- `MemoryLabelFormatSingular=Vor {0} Jahr`

## Architecture

### Backend Changes

**`IGeneralSettings.cs`** - Add two new properties:
```csharp
bool GroupMemories { get; }
string? MemoryLabelFormat { get; }
string? MemoryLabelFormatSingular { get; }
```

**`CachingApiAssetsPool.cs`** - Add `PreserveOrder` mechanism:
- New virtual property `protected virtual bool PreserveOrder => false;`
- `GetAssets()` checks `PreserveOrder`: if `true`, returns assets in their original order (no `OrderBy(_ => _random.Next())`); if `false`, shuffles as before.

**`MemoryAssetsPool.cs`** - Conditional grouping and sorting:
- Constructor receives `IGeneralSettings` (in addition to existing params) to access `GroupMemories` and `MemoryLabelFormat`.
- `override bool PreserveOrder => _generalSettings.GroupMemories;`
- When `GroupMemories = true`: after loading memories from the API, sort by `yearsAgo` ascending (1 year first, then 2, etc.). Within a year group, maintain Immich's original order.
- When `GroupMemories = false`: existing behavior (no sorting, will be shuffled by base class).
- Description text: use `MemoryLabelFormat` if set (replace `{0}` with year count, handle singular for count=1). Fall back to existing English `"X year(s) ago"` if not set.

**`ServerSettings.cs` / env var mapping** - Map the two new env vars to the settings properties.

**`ClientSettingsDto` (API response)** - Add `groupMemories` and `memoryLabelFormat` fields so the frontend knows the state.

### Frontend Changes

**`immichFrameApi.ts` (`ClientSettingsDto` type):**
```typescript
groupMemories?: boolean;
memoryLabelFormat?: string | null;
memoryLabelFormatSingular?: string | null;
```

**New component `memory-label.svelte`:**
- Positioned in the clock area (bottom-left), above the time display.
- Shows the Memory year text extracted from `asset.exifInfo.description`.
- Font size: `text-xl sm:text-xl md:text-2xl lg:text-4xl` (smaller than clock time which uses `text-4xl` to `text-8xl`).
- Uses the same styling system as clock (solid/transition/blur based on `$configStore.style`).
- Visible only when:
  - `$configStore.groupMemories === true`
  - `$configStore.memoryLabelFormat` is set
  - Current asset's `exifInfo.description` matches the Memory label pattern

**`home-page.svelte` integration:**
- Import and render `<MemoryLabel>` next to `<Clock>`.
- Pass the current displaying asset(s) as prop.
- The label component reads `description` from the current asset to determine the text.

**`asset-info.svelte` adjustment:**
- When Memory label is active and showing, suppress the `description` row in the bottom-right info panel to avoid duplicate text.

## Data Flow

```
Immich API  -->  MemoryAssetsPool.LoadAssets()
                   |
                   v
            Sort by yearsAgo (if GroupMemories=true)
            Set description = MemoryLabelFormat.replace({0}, yearsAgo)
                   |
                   v
            CachingApiAssetsPool.GetAssets()
            (PreserveOrder=true: no shuffle; false: shuffle)
                   |
                   v
            MultiAssetPool.GetNextAsset()
            (picks from Memory pool proportionally with others)
                   |
                   v
            API Response  -->  Frontend assetBacklog
                   |
                   v
            home-page.svelte: next asset from backlog
                   |
                   +-->  memory-label.svelte: show "Vor X Jahren" if Memory
                   +-->  asset-info.svelte: suppress description if Memory label active
```

## Files to Modify

| File | Change |
|------|--------|
| `ImmichFrame.Core/Interfaces/IServerSettings.cs` | Add `GroupMemories`, `MemoryLabelFormat` to `IGeneralSettings` |
| `ImmichFrame.Core/Logic/Pool/CachingApiAssetsPool.cs` | Add `PreserveOrder` virtual property, conditional shuffle |
| `ImmichFrame.Core/Logic/Pool/MemoryAssetsPool.cs` | Accept `IGeneralSettings`, sort by year, use format string, override `PreserveOrder` |
| `ImmichFrame.WebApi/Models/ServerSettings.cs` (or equivalent) | Map env vars to new properties |
| `ImmichFrame.WebApi/Models/ClientSettingsDto.cs` (or equivalent) | Add frontend-facing fields |
| `immichFrame.Web/src/lib/immichFrameApi.ts` | Add types to `ClientSettingsDto` |
| `immichFrame.Web/src/lib/components/elements/memory-label.svelte` | New component |
| `immichFrame.Web/src/lib/components/home-page/home-page.svelte` | Integrate Memory label |
| `immichFrame.Web/src/lib/components/elements/asset-info.svelte` | Suppress duplicate description |

## Edge Cases

- **No Memories today**: Memory pool is empty, no Memory blocks shown, label never appears. Normal behavior.
- **Only 1 year of Memories**: Single group, label shows "Vor 1 Jahr", then normal images resume.
- **`GroupMemories=true` but `MemoryLabelFormat` not set**: Memories are still grouped/sorted, but no label overlay is shown. The English "X years ago" description still appears in asset-info.
- **`MemoryLabelFormat` set but `GroupMemories=false`**: The label format is used for the description text, but images are shuffled (not grouped). Label overlay is still shown per-image.
- **Mixed sources (Favorites + Memories + Albums)**: MultiAssetPool picks proportionally. When it picks from the Memory pool, it gets a block of sorted images. The Memory label appears/disappears as the source switches.

## Testing

- Verify with `GroupMemories=false` (default): behavior identical to current version.
- Verify with `GroupMemories=true`, `MemoryLabelFormat=Vor {0} Jahren`: Memories appear in year-sorted blocks, label visible.
- Verify singular handling: "Vor 1 Jahr" vs "Vor 3 Jahren".
- Verify label disappears when non-Memory images are shown.
- Verify existing unit tests still pass.
