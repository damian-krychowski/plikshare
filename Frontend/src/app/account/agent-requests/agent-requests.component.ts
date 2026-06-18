import { Component, OnInit, signal } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AgentRequestsService } from '../../services/agent-requests.service';
import { AgentOperationDetails, AgentsApi, PendingAgentOperation } from '../../services/agents.api';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { RelativeTimeComponent } from '../../shared/relative-time/relative-time.component';
import { ConfirmOperationDirective } from '../../shared/operation-confirm/confirm-operation.directive';
import { AgentOperationDetailsComponent } from '../../shared/agent-operation-details/agent-operation-details.component';

@Component({
    selector: 'app-agent-requests',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        ActionButtonComponent,
        RelativeTimeComponent,
        ConfirmOperationDirective,
        AgentOperationDetailsComponent
    ],
    templateUrl: './agent-requests.component.html',
    styleUrl: './agent-requests.component.scss'
})
export class AgentRequestsComponent implements OnInit {
    // Tools whose approval details carry no data beyond what the card header already shows — the card
    // title/workspace says it all, so there's nothing worth expanding.
    private static readonly TOOLS_WITHOUT_DETAILS = new Set<string>([
        'list_workspaces',
        'list_storages',
        'list_share_links'
    ]);

    isInitialized = signal(false);

    expandedId = signal<string | null>(null);
    private _details = signal<Map<string, AgentOperationDetails>>(new Map());

    hasDetails(op: PendingAgentOperation): boolean {
        return !AgentRequestsComponent.TOOLS_WITHOUT_DETAILS.has(op.toolName);
    }

    constructor(
        public agentRequests: AgentRequestsService,
        private _agentsApi: AgentsApi,
        private _location: Location,
        private _router: Router,
        private _route: ActivatedRoute) {
    }

    detailsOf(operationExternalId: string): AgentOperationDetails | undefined {
        return this._details().get(operationExternalId);
    }

    async toggle(op: PendingAgentOperation) {
        if (!this.hasDetails(op))
            return;

        if (this.expandedId() === op.externalId) {
            this.expandedId.set(null);
            this.syncExpandedToUrl(null);
            return;
        }

        this.syncExpandedToUrl(op.externalId);
        await this.open(op.externalId);
    }

    private async open(operationExternalId: string) {
        this.expandedId.set(operationExternalId);

        if (this._details().has(operationExternalId))
            return;

        try {
            const details = await this._agentsApi.getOperationDetails(operationExternalId);

            this._details.update(map => {
                const next = new Map(map);
                next.set(operationExternalId, details);
                return next;
            });
        } catch (error) {
            console.error(error);
            this.expandedId.set(null);
            this.syncExpandedToUrl(null);
        }
    }

    // Keep the expanded request in the URL so coming back from the explorer re-opens it in place,
    // without piling up history entries (replaceUrl).
    private syncExpandedToUrl(expanded: string | null) {
        this._router.navigate([], {
            relativeTo: this._route,
            queryParams: { expanded },
            queryParamsHandling: 'merge',
            replaceUrl: true
        });
    }

    async ngOnInit(): Promise<void> {
        try {
            await this.agentRequests.refresh();

            const expanded = this._route.snapshot.queryParamMap.get('expanded');

            if (expanded)
                await this.open(expanded);
        } finally {
            this.isInitialized.set(true);
        }
    }

    goBack() {
        this._location.back();
    }

    toolLabel(toolName: string): string {
        const spaced = toolName.replace(/_/g, ' ');
        return spaced.charAt(0).toUpperCase() + spaced.slice(1);
    }

    summary(op: PendingAgentOperation): string {
        if (op.toolName === 'bulk_delete') {
            const folders = op.parameters?.folderExternalIds?.length ?? 0;
            const files = op.parameters?.fileExternalIds?.length ?? 0;

            const parts: string[] = [];

            if (folders > 0)
                parts.push(`${folders} ${folders === 1 ? 'folder' : 'folders'}`);

            if (files > 0)
                parts.push(`${files} ${files === 1 ? 'file' : 'files'}`);

            return parts.length > 0 ? parts.join(', ') : 'nothing selected';
        }

        return this.toolLabel(op.toolName);
    }

    async approve(op: PendingAgentOperation) {
        this.agentRequests.remove(op.externalId);

        try {
            await this._agentsApi.approveOperation(op.agent.externalId, op.externalId);
        } catch (error) {
            console.error(error);
            await this.agentRequests.refresh();
        }
    }

    async deny(op: PendingAgentOperation) {
        this.agentRequests.remove(op.externalId);

        try {
            await this._agentsApi.denyOperation(op.agent.externalId, op.externalId);
        } catch (error) {
            console.error(error);
            await this.agentRequests.refresh();
        }
    }
}
