import type { AppFileItem } from '../../shared/file-item/file-item.component';
import type { AppFolderItem } from '../../shared/folder-item/folder-item.component';
import type { GalleryTile } from '../files-gallery/files-gallery.component';

export type MinimapBlockKind = 'tile' | 'row' | 'folder' | 'header';

export type MinimapBlock = {
    xf: number;
    wf: number;
    y: number;
    h: number;
    kind: MinimapBlockKind;
    id: string | null;
    label: string | null;
    thumbUrl: string | null;
    showCheckbox: boolean;
};

export type MinimapModel = {
    blocks: MinimapBlock[];
    modelHeight: number;
};

export type MinimapItemState = {
    selectedIds: ReadonlySet<string>;
    cutIds: ReadonlySet<string>;
};

export type MinimapItemRef = {
    id: string;
    kind: MinimapBlockKind;
};

export type MinimapSegment = {
    key: string;
    label: string | null;
    sectionEl: HTMLElement | null;
    contentEl: HTMLElement | null;
    model: MinimapModel;
    itemState?: () => MinimapItemState;
};

export const EMPTY_MINIMAP_MODEL: MinimapModel = {
    blocks: [],
    modelHeight: 0
};

export const EMPTY_MINIMAP_ITEM_STATE: MinimapItemState = {
    selectedIds: new Set<string>(),
    cutIds: new Set<string>()
};

const ROW_MIN_WIDTH_FRACTION = 0.22;
const ROW_MAX_WIDTH_FRACTION = 0.92;
const ROW_WIDTH_PER_CHAR_FRACTION = 0.02;

function rowWidthFraction(nameLength: number): number {
    return Math.min(
        ROW_MAX_WIDTH_FRACTION,
        ROW_MIN_WIDTH_FRACTION + nameLength * ROW_WIDTH_PER_CHAR_FRACTION);
}

export function buildMinimapItemState(
    items: { externalId: string, isSelected: () => boolean, isCut: () => boolean }[]
): MinimapItemState {
    const selectedIds = new Set<string>();
    const cutIds = new Set<string>();

    for (const item of items) {
        if (item.isSelected())
            selectedIds.add(item.externalId);

        if (item.isCut())
            cutIds.add(item.externalId);
    }

    return { selectedIds, cutIds };
}

export function foldersToMinimapModel(args: {
    folders: AppFolderItem[];
    rowHeight: number;
    showCheckboxes: boolean;
}): MinimapModel {
    const { folders, rowHeight, showCheckboxes } = args;

    const blocks: MinimapBlock[] = folders.map((folder, index) => {
        const name = folder.name();

        return {
            xf: 0,
            wf: rowWidthFraction(name.length),
            y: index * rowHeight,
            h: rowHeight,
            kind: 'folder',
            id: folder.externalId,
            label: name,
            thumbUrl: null,
            showCheckbox: showCheckboxes
        };
    });

    return {
        blocks,
        modelHeight: folders.length * rowHeight
    };
}

export function buildMiniThumbUrl(
    file: AppFileItem,
    getThumbnailUrl: ((fileExternalId: string) => string) | undefined
): string | null {
    const miniEtag = file.metadata()?.thumbnail?.miniEtag;

    if (!miniEtag)
        return null;

    const base = getThumbnailUrl?.(file.externalId);

    if (!base)
        return null;

    const separator = base.includes('?') ? '&' : '?';

    return `${base}${separator}v=${miniEtag}`;
}

export function fileRowsToMinimapModel(args: {
    files: AppFileItem[];
    rowHeight: number;
    totalHeight: number;
    buildThumbUrl: ((file: AppFileItem) => string | null) | null;
    showCheckboxes: boolean;
}): MinimapModel {
    const { files, rowHeight, totalHeight, buildThumbUrl, showCheckboxes } = args;

    const blocks: MinimapBlock[] = files.map((file, index) => {
        const fullName = file.name() + file.extension;

        return {
            xf: 0,
            wf: rowWidthFraction(fullName.length),
            y: index * rowHeight,
            h: rowHeight,
            kind: 'row',
            id: file.externalId,
            label: fullName,
            thumbUrl: buildThumbUrl?.(file) ?? null,
            showCheckbox: showCheckboxes
        };
    });

    return {
        blocks,
        modelHeight: Math.max(
            totalHeight,
            files.length * rowHeight)
    };
}

export function galleryToMinimapModel(args: {
    tiles: GalleryTile[];
    headers: { label: string; y: number }[];
    headerHeight: number;
    contentWidth: number;
    contentHeight: number;
    buildThumbUrl: (file: AppFileItem) => string | null;
}): MinimapModel {
    const { tiles, headers, headerHeight, contentWidth, contentHeight, buildThumbUrl } = args;

    if (contentWidth <= 0) {
        return EMPTY_MINIMAP_MODEL;
    }

    const blocks: MinimapBlock[] = [];

    for (const header of headers) {
        blocks.push({
            xf: 0,
            wf: 1,
            y: header.y,
            h: headerHeight,
            kind: 'header',
            id: null,
            label: header.label,
            thumbUrl: null,
            showCheckbox: false
        });
    }

    for (const tile of tiles) {
        blocks.push({
            xf: tile.x / contentWidth,
            wf: tile.w / contentWidth,
            y: tile.y,
            h: tile.h,
            kind: 'tile',
            id: tile.file.externalId,
            label: tile.file.name() + tile.file.extension,
            thumbUrl: buildThumbUrl(tile.file),
            showCheckbox: false
        });
    }

    blocks.sort((a, b) => a.y - b.y);

    return {
        blocks,
        modelHeight: contentHeight
    };
}
