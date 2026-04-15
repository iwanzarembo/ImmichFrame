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
