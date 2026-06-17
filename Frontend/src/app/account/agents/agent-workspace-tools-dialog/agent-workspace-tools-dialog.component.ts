import { Component, Inject, OnInit, computed, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { MatSelectSearchComponent } from 'ngx-mat-select-search';
import { ActionButtonComponent } from '../../../shared/buttons/action-btn/action-btn.component';
import { AgentsApi, AgentWorkspaceToolConfig } from '../../../services/agents.api';

export interface AgentWorkspaceToolsDialogData {
    agentExternalId: string;
    workspaceExternalId: string;
    workspaceName: string;
}

@Component({
    selector: 'app-agent-workspace-tools-dialog',
    imports: [
        FormsModule,
        MatButtonModule,
        MatSelectModule,
        MatSlideToggleModule,
        MatTooltipModule,
        MatSelectSearchComponent,
        ActionButtonComponent
    ],
    templateUrl: './agent-workspace-tools-dialog.component.html',
    styleUrl: './agent-workspace-tools-dialog.component.scss'
})
export class AgentWorkspaceToolsDialogComponent implements OnInit {
    isLoading = signal(false);
    workspaceTools = signal<AgentWorkspaceToolConfig[]>([]);
    addedTools = signal<Set<string>>(new Set<string>());

    visibleOverrides = computed(() =>
        this.workspaceTools().filter(t => this.hasOverride(t) || this.addedTools().has(t.name)));

    availableToAdd = computed(() =>
        this.workspaceTools().filter(t => !this.hasOverride(t) && !this.addedTools().has(t.name)));

    addSearch = signal('');

    filteredAvailableToAdd = computed(() => {
        const search = this.addSearch().toLowerCase();
        const available = this.availableToAdd();

        if (!search)
            return available;

        return available.filter(t =>
            t.name.toLowerCase().includes(search) || t.description.toLowerCase().includes(search));
    });

    constructor(
        @Inject(MAT_DIALOG_DATA) public data: AgentWorkspaceToolsDialogData,
        public dialogRef: MatDialogRef<AgentWorkspaceToolsDialogComponent, boolean>,
        private _agentsApi: AgentsApi) {}

    async ngOnInit() {
        await this.load();
    }

    private async load() {
        this.isLoading.set(true);

        try {
            const result = await this._agentsApi.getAgentWorkspaceTools(
                this.data.agentExternalId, this.data.workspaceExternalId);
            this.workspaceTools.set(result.tools);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    hasOverride(tool: AgentWorkspaceToolConfig): boolean {
        return tool.overrideIsEnabled !== null || tool.overrideRequiresApproval !== null;
    }

    onAddOverride(toolName: string) {
        if (!toolName)
            return;

        this.addedTools.update(set => new Set(set).add(toolName));
        this.addSearch.set('');
    }

    async onToggleEnabled(tool: AgentWorkspaceToolConfig) {
        await this.save(tool, !tool.effectiveIsEnabled, tool.effectiveRequiresApproval);
    }

    async onToggleApproval(tool: AgentWorkspaceToolConfig) {
        await this.save(tool, tool.effectiveIsEnabled, !tool.effectiveRequiresApproval);
    }

    async onRemoveOverride(tool: AgentWorkspaceToolConfig) {
        this.addedTools.update(set => {
            const next = new Set(set);
            next.delete(tool.name);
            return next;
        });

        try {
            await this._agentsApi.resetAgentWorkspaceToolOverride(
                this.data.agentExternalId, this.data.workspaceExternalId, tool.name);
        } catch (error) {
            console.error(error);
        }

        await this.load();
    }

    onDone() {
        this.dialogRef.close(true);
    }

    private async save(tool: AgentWorkspaceToolConfig, isEnabled: boolean, requiresApproval: boolean) {
        try {
            await this._agentsApi.updateAgentWorkspaceToolOverride(
                this.data.agentExternalId, this.data.workspaceExternalId, tool.name, {
                    isEnabled,
                    requiresApproval
                });
        } catch (error) {
            console.error(error);
        }

        await this.load();
    }
}
