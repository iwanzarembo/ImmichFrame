using ImmichFrame.Core.Api;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// Wraps a primary pool with a fallback: if the primary pool has no assets,
/// delegates to the fallback pool instead. Once the primary pool is determined
/// to be empty (or non-empty), that decision is cached for the lifetime of this instance.
/// </summary>
public class FallbackAssetPool(IAssetPool primary, IAssetPool fallback) : IAssetPool
{
    private bool? _primaryIsEmpty;

    public async Task<long> GetAssetCount(CancellationToken ct = default)
    {
        var pool = await GetActivePool(ct);
        return await pool.GetAssetCount(ct);
    }

    public async Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default)
    {
        var pool = await GetActivePool(ct);
        return await pool.GetAssets(requested, ct);
    }

    private async Task<IAssetPool> GetActivePool(CancellationToken ct)
    {
        if (_primaryIsEmpty == null)
        {
            var count = await primary.GetAssetCount(ct);
            _primaryIsEmpty = count == 0;
        }

        return _primaryIsEmpty.Value ? fallback : primary;
    }
}
