import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface TrashItemDto {
    externalId: string;
    name: string;
    extension: string;
    sizeInBytes: number;
    deletedAt: string;

    // When the retention sweeper is expected to permanently remove this item.
    // Null when the workspace keeps trash forever (enabled, no retention limit).
    autoDeletesAt: string | null;

    // Folder names root → leaf where the file lived before being trashed.
    // Null for files trashed straight from the workspace root.
    originalFolderPath: string[] | null;
}

export interface GetTrashItemsResponse {
    items: TrashItemDto[];
    totalSizeInBytes: number;
}

export type RestoreMode = 'original-path' | 'chosen-folder';

export type RestoreStatus = 'restored' | 'not-found' | 'destination-invalid';

export interface RestoreItem {
    fileExternalId: string;
    mode: RestoreMode;

    // Required for 'chosen-folder'; a null target means the workspace root.
    // Ignored for 'original-path' (the snapshot decides the path).
    targetFolderExternalId: string | null;
}

export interface RestoreFromTrashRequest {
    items: RestoreItem[];
}

export interface RestoreItemResult {
    fileExternalId: string;
    status: RestoreStatus;
}

export interface RestoreFromTrashResponse {
    results: RestoreItemResult[];
}

export interface DeleteForeverRequest {
    fileExternalIds: string[];
}

export interface DeleteForeverResponse {
    deletedCount: number;
    newWorkspaceSizeInBytes: number;
}

@Injectable({
    providedIn: 'root'
})
export class TrashApi {
    constructor(
        private _http: HttpClient) {
    }

    public async getItems(workspaceExternalId: string): Promise<GetTrashItemsResponse> {
        const call = this
            ._http
            .get<GetTrashItemsResponse>(
                `/api/workspaces/${workspaceExternalId}/trash`);

        return await firstValueFrom(call);
    }

    public async restore(workspaceExternalId: string, request: RestoreFromTrashRequest): Promise<RestoreFromTrashResponse> {
        const call = this
            ._http
            .post<RestoreFromTrashResponse>(
                `/api/workspaces/${workspaceExternalId}/trash/restore`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async deleteForever(workspaceExternalId: string, request: DeleteForeverRequest): Promise<DeleteForeverResponse> {
        const call = this
            ._http
            .post<DeleteForeverResponse>(
                `/api/workspaces/${workspaceExternalId}/trash/items/delete-forever`, request, {
                headers: new HttpHeaders({
                    'Content-Type': 'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async emptyTrash(workspaceExternalId: string): Promise<DeleteForeverResponse> {
        const call = this
            ._http
            .post<DeleteForeverResponse>(
                `/api/workspaces/${workspaceExternalId}/trash/empty`, {});

        return await firstValueFrom(call);
    }
}
