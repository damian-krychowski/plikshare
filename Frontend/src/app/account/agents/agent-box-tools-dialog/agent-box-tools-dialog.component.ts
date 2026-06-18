import { Component, Inject, OnInit, computed, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { MatSelectSearchComponent } from 'ngx-mat-select-search';
import { ActionButtonComponent } from '../../../shared/buttons/action-btn/action-btn.component';
import { AgentsApi, AgentBoxToolConfig } from '../../../services/agents.api';

export interface AgentBoxToolsDialogData {
    agentExternalId: string;
    boxExternalId: string;
    boxName: string;
}

@Component({
    selector: 'app-agent-box-tools-dialog',
    imports: [
        FormsModule,
        MatButtonModule,
        MatSelectModule,
        MatSlideToggleModule,
        MatTooltipModule,
        MatSelectSearchComponent,
        ActionButtonComponent
    ],
    templateUrl: './agent-box-tools-dialog.component.html',
    styleUrl: './agent-box-tools-dialog.component.scss'
})
export class AgentBoxToolsDialogComponent implements OnInit {
    isLoading = signal(false);
    boxTools = signal<AgentBoxToolConfig[]>([]);
    addedTools = signal<Set<string>>(new Set<string>());

    visibleOverrides = computed(() =>
        this.boxTools().filter(t => this.hasOverride(t) || this.addedTools().has(t.name)));

    availableToAdd = computed(() =>
        this.boxTools().filter(t => !this.hasOverride(t) && !this.addedTools().has(t.name)));

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
        @Inject(MAT_DIALOG_DATA) public data: AgentBoxToolsDialogData,
        public dialogRef: MatDialogRef<AgentBoxToolsDialogComponent, boolean>,
        private _agentsApi: AgentsApi) {}

    async ngOnInit() {
        await this.load();
    }

    private async load() {
        this.isLoading.set(true);

        try {
            const result = await this._agentsApi.getAgentBoxTools(
                this.data.agentExternalId, this.data.boxExternalId);
            this.boxTools.set(result.tools);
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    hasOverride(tool: AgentBoxToolConfig): boolean {
        return tool.overrideIsEnabled !== null || tool.overrideRequiresApproval !== null;
    }

    onAddOverride(toolName: string) {
        if (!toolName)
            return;

        this.addedTools.update(set => new Set(set).add(toolName));
        this.addSearch.set('');
    }

    async onToggleEnabled(tool: AgentBoxToolConfig) {
        await this.save(tool, !tool.effectiveIsEnabled, tool.effectiveRequiresApproval);
    }

    async onToggleApproval(tool: AgentBoxToolConfig) {
        await this.save(tool, tool.effectiveIsEnabled, !tool.effectiveRequiresApproval);
    }

    async onRemoveOverride(tool: AgentBoxToolConfig) {
        this.addedTools.update(set => {
            const next = new Set(set);
            next.delete(tool.name);
            return next;
        });

        try {
            await this._agentsApi.resetAgentBoxToolOverride(
                this.data.agentExternalId, this.data.boxExternalId, tool.name);
        } catch (error) {
            console.error(error);
        }

        await this.load();
    }

    onDone() {
        this.dialogRef.close(true);
    }

    private async save(tool: AgentBoxToolConfig, isEnabled: boolean, requiresApproval: boolean) {
        try {
            await this._agentsApi.updateAgentBoxToolOverride(
                this.data.agentExternalId, this.data.boxExternalId, tool.name, {
                    isEnabled,
                    requiresApproval
                });
        } catch (error) {
            console.error(error);
        }

        await this.load();
    }
}
