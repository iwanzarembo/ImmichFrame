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
            var yearsAgo = searchDate.Year - (int)memory.Data.Year;

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
