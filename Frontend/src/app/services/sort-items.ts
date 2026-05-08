import { AppFileItem } from '../shared/file-item/file-item.component';
import { AppFolderItem } from '../shared/folder-item/folder-item.component';
import { SortDirection, SortMode } from './folders-and-files.api';

export function sortFolders(folders: AppFolderItem[], mode: SortMode, direction: SortDirection): AppFolderItem[] {
    const sorted = [...folders];
    const sign = direction === 'asc' ? 1 : -1;

    if (mode === 'name') {
        sorted.sort((a, b) => sign * a.name().localeCompare(b.name(), undefined, { sensitivity: 'base' }));
    } else if (mode === 'date') {
        sorted.sort((a, b) => sign * ((a.createdAt?.getTime() ?? 0) - (b.createdAt?.getTime() ?? 0)));
    } else {
        sorted.sort((a, b) => a.position() - b.position());
    }

    return sorted;
}

export function sortFiles(files: AppFileItem[], mode: SortMode, direction: SortDirection): AppFileItem[] {
    const sorted = [...files];
    const sign = direction === 'asc' ? 1 : -1;

    if (mode === 'name') {
        sorted.sort((a, b) => sign * a.name().localeCompare(b.name(), undefined, { sensitivity: 'base' }));
    } else if (mode === 'date') {
        sorted.sort((a, b) => sign * ((a.createdAt?.getTime() ?? 0) - (b.createdAt?.getTime() ?? 0)));
    } else {
        sorted.sort((a, b) => a.position() - b.position());
    }

    return sorted;
}
