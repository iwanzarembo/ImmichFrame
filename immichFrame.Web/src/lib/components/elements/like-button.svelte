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
	let loading = $state(false);

	let liked = $derived(alreadyLiked || justLiked);

	async function handleLike() {
		if (loading || liked) return;
		loading = true;
		try {
			await api.likeAsset(assetId, { clientIdentifier: $clientIdentifierStore });
			justLiked = true;
		} catch (err) {
			console.error('Failed to like asset:', err);
		} finally {
			loading = false;
		}
	}

	$effect(() => {
		// Reset justLiked state when asset changes
		assetId;
		justLiked = false;
	});
</script>

{#if $configStore.likeAlbum}
	<button
		class="like-button fixed bottom-16 right-4 z-[110] text-primary drop-shadow-2xl transition-opacity duration-300"
		class:opacity-0={!visible}
		class:opacity-70={visible && !liked}
		class:opacity-100={liked}
		onclick={handleLike}
		disabled={loading || liked}
	>
		<Icon
			path={liked ? mdiHeart : mdiHeartOutline}
			size="2.5rem"
			color={liked ? '#ef4444' : 'currentColor'}
			title={liked ? 'Already liked' : 'Like'}
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
		cursor: default;
	}
	.like-button:hover:not(:disabled) {
		opacity: 1 !important;
	}
</style>
