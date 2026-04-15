<script lang="ts">
	import * as api from '$lib/index';
	import { configStore } from '$lib/stores/config.store';
	import { clientIdentifierStore } from '$lib/stores/persist.store';
	import { mdiHeartOutline, mdiHeart } from '@mdi/js';
	import Icon from './icon.svelte';

	interface Props {
		assetId: string;
		albums: api.AlbumResponseDto[];
		visible: boolean;
	}

	let { assetId, albums, visible }: Props = $props();

	let alreadyLiked = $derived(
		albums.some((a) => a.albumName === $configStore.likeAlbum)
	);
	let justLiked = $state(false);
	let justUnliked = $state(false);
	let loading = $state(false);

	let liked = $derived((alreadyLiked && !justUnliked) || justLiked);

	async function handleClick() {
		if (loading) return;
		loading = true;
		try {
			if (liked) {
				await api.unlikeAsset(assetId, { clientIdentifier: $clientIdentifierStore });
				justLiked = false;
				justUnliked = true;
			} else {
				await api.likeAsset(assetId, { clientIdentifier: $clientIdentifierStore });
				justLiked = true;
				justUnliked = false;
			}
		} catch (err) {
			console.error('Failed to update like status:', err);
		} finally {
			loading = false;
		}
	}

	$effect(() => {
		// Reset local state when asset changes
		assetId;
		justLiked = false;
		justUnliked = false;
	});
</script>

{#if $configStore.likeAlbum}
	<button
		class="like-button fixed bottom-16 right-4 z-[110] text-primary drop-shadow-2xl transition-opacity duration-300"
		class:opacity-0={!visible}
		class:opacity-70={visible && !liked}
		class:opacity-100={liked}
		onclick={handleClick}
		disabled={loading}
	>
		<Icon
			path={liked ? mdiHeart : mdiHeartOutline}
			size="2.5rem"
			color={liked ? '#ef4444' : 'currentColor'}
			title={liked ? 'Unlike' : 'Like'}
		/>
	</button>
{/if}

<style>
	.like-button {
		background: none;
		border: none;
		cursor: pointer;
		padding: 0.5rem;
	}
	.like-button:disabled {
		cursor: wait;
	}
	.like-button:hover:not(:disabled) {
		opacity: 1 !important;
	}
</style>
