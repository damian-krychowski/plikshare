import { computed, Injectable, signal, WritableSignal } from '@angular/core';
import { WorkspaceDto } from '../services/workspaces.api';

@Injectable({
    providedIn: 'root'
})
export class WorkspaceContextService {
    workspace: WritableSignal<WorkspaceDto | null> = signal(null);
    integrations = computed(() => this.workspace()?.integrations ?? {textract: null, chatGpt: []});

    updateWorkspaceSize(value: number) {
        this.workspace.update(workspace => {
            if(workspace == null)
                return null;

            return {
                currentSizeInBytes: value,

                externalId: workspace.externalId,
                integrations: workspace.integrations,
                maxSizeInBytes: workspace.maxSizeInBytes,
                name: workspace.name,
                owner: workspace.owner,
                pendingUploadsCount: workspace.pendingUploadsCount,
                permissions: workspace.permissions
            };
        });
    }
}