using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Logic.Pool;

public class LikeAlbumAssetsPool(IApiCache apiCache, ImmichApi immichApi, IAccountSettings accountSettings, string albumName) : CachingApiAssetsPool(apiCache, immichApi, accountSettings)
{
    protected override async Task<IEnumerable<AssetResponseDto>> LoadAssets(CancellationToken ct = default)
    {
        var albums = await immichApi.GetAllAlbumsAsync(null, null, ct);
        var album = albums.FirstOrDefault(a => a.AlbumName == albumName);
        if (album == null)
        {
            return Enumerable.Empty<AssetResponseDto>();
        }

        var albumInfo = await immichApi.GetAlbumInfoAsync(new Guid(album.Id), null, null, ct);
        return albumInfo.Assets;
    }
}
