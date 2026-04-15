<script lang="ts">
	import * as api from '$lib/index';
	import { configStore } from '$lib/stores/config.store';
	import { clientIdentifierStore } from '$lib/stores/persist.store';
	import { mdiHeartOutline, mdiHeart } from '@mdi/js';
	import Icon from './icon.svelte';

	interface Props {
		assetId: string;
		visible: boolean;
	}

	let { assetId, visible }: Props = $props();

	let liked = $state(false);
	let loading = $state(false);

	async function handleLike() {
		if (loading) return;
		loading = true;
		try {
			await api.likeAsset(assetId, { clientIdentifier: $clientIdentifierStore });
			liked = true;
			setTimeout(() => {
				liked = false;
			}, 2000);
		} catch (err) {
			console.error('Failed to like asset:', err);
		} finally {
			loading = false;
		}
	}

	$effect(() => {
		// Reset liked state when asset changes
		assetId;
		liked = false;
	});
</script>

{#if $configStore.likeAlbum}
	<button
		class="like-button fixed bottom-16 right-4 z-50 text-primary drop-shadow-2xl transition-opacity duration-300"
		class:opacity-0={!visible}
		class:opacity-70={visible && !liked}
		class:opacity-100={liked}
		onclick={handleLike}
		disabled={loading}
	>
		<Icon
			path={liked ? mdiHeart : mdiHeartOutline}
			size="2.5rem"
			color={liked ? '#ef4444' : 'currentColor'}
			title="Like"
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
	.like-button:hover {
		opacity: 1 !important;
	}
</style>
