import { AppFileItem } from '../shared/file-item/file-item.component';
import { AppFolderItem } from '../shared/folder-item/folder-item.component';
import { SortDirection, SortMode } from './folders-and-files.api';

const nameCollator = new Intl.Collator(undefined, { sensitivity: 'base' });

type SortableItem = {
    name: () => string;
    createdAt: Date | null;
    position: () => number;
};

function sortItemsInPlace<T extends SortableItem>(
    items: T[],
    mode: SortMode,
    direction: SortDirection
): void {
    const sign = direction === 'asc' ? 1 : -1;

    if (mode === 'name') {
        const keys = new Map<T, string>();

        for (const item of items) {
            keys.set(item, item.name());
        }

        items.sort((a, b) => sign * nameCollator.compare(keys.get(a)!, keys.get(b)!));
    } else if (mode === 'date') {
        const keys = new Map<T, number>();

        for (const item of items) {
            keys.set(item, item.createdAt?.getTime() ?? 0);
        }

        items.sort((a, b) => sign * (keys.get(a)! - keys.get(b)!));
    } else {
        const keys = new Map<T, number>();

        for (const item of items) {
            keys.set(item, item.position());
        }

        items.sort((a, b) => keys.get(a)! - keys.get(b)!);
    }
}

export function sortFolders(folders: AppFolderItem[], mode: SortMode, direction: SortDirection): AppFolderItem[] {
    const sorted = [...folders];

    sortFoldersInPlace(sorted, mode, direction);

    return sorted;
}

export function sortFoldersInPlace(folders: AppFolderItem[], mode: SortMode, direction: SortDirection): void {
    sortItemsInPlace(folders, mode, direction);
}

export function sortFiles(files: AppFileItem[], mode: SortMode, direction: SortDirection): AppFileItem[] {
    const sorted = [...files];

    sortFilesInPlace(sorted, mode, direction);

    return sorted;
}

export function sortFilesInPlace(files: AppFileItem[], mode: SortMode, direction: SortDirection): void {
    sortItemsInPlace(files, mode, direction);
}
