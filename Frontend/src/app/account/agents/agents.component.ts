import { Router } from "@angular/router";
import { Component, OnInit, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatDialog } from "@angular/material/dialog";
import { AuthService } from "../../services/auth.service";
import { insertItem, removeItem } from "../../shared/signal-utils";
import { OptimisticOperation } from "../../services/optimistic-operation";
import { AgentsApi } from "../../services/agents.api";
import { AppAgent, AgentItemComponent } from "../../shared/agent-item/agent-item.component";
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { ItemButtonComponent } from "../../shared/buttons/item-btn/item-btn.component";
import { AgentCreateDialogComponent } from "./agent-create-dialog/agent-create-dialog.component";
import { AgentTokenDialogComponent, AgentTokenDialogData } from "./agent-token-dialog/agent-token-dialog.component";

@Component({
    selector: 'app-agents',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        AgentItemComponent,
        ActionButtonComponent,
        ItemButtonComponent
    ],
    templateUrl: './agents.component.html',
    styleUrl: './agents.component.scss'
})
export class AgentsComponent implements OnInit {
    isLoading = signal(false);
    isInitialized = signal(false);

    agents: WritableSignal<AppAgent[]> = signal([]);

    constructor(
        public auth: AuthService,
        private _agentsApi: AgentsApi,
        private _router: Router,
        private _dialog: MatDialog
    ) {}

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            await this.loadAgents();
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
            this.isInitialized.set(true);
        }
    }

    private async loadAgents() {
        const result = await this._agentsApi.getAgents();

        this.agents.set(result.items.map(a => this.mapAgent(a)));
    }

    private mapAgent(a: { externalId: string, name: string, isEnabled: boolean, createdAt: string }): AppAgent {
        return {
            externalId: a.externalId,
            name: signal(a.name),
            isEnabled: signal(a.isEnabled),
            createdAt: a.createdAt,
            isHighlighted: signal(false)
        };
    }

    goToAccount() {
        this._router.navigate(['account']);
    }

    goToAgent(agent: AppAgent) {
        this._router.navigate(['settings/agents', agent.externalId]);
    }

    onCreateAgent() {
        const dialogRef = this._dialog.open(AgentCreateDialogComponent, {
            width: '500px',
            maxHeight: '80vh',
            position: {
                top: '100px'
            }
        });

        dialogRef.afterClosed().subscribe((name: string | undefined) => {
            if (!name)
                return;

            this.createAgent(name);
        });
    }

    private async createAgent(name: string) {
        try {
            this.isLoading.set(true);

            const result = await this._agentsApi.createAgent({ name });

            const created = this.mapAgent({
                externalId: result.externalId,
                name,
                isEnabled: true,
                createdAt: new Date().toISOString()
            });

            this.agents.update(items => [created, ...items]);

            const data: AgentTokenDialogData = {
                title: 'Agent created',
                token: result.token
            };

            this._dialog.open(AgentTokenDialogComponent, {
                width: '700px',
                maxWidth: '95vw',
                position: { top: '80px' },
                disableClose: true,
                data
            });
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    async onAgentDelete(operation: OptimisticOperation, agent: AppAgent) {
        const itemRemoved = removeItem(this.agents, agent);

        const result = await operation.wait();

        if (result.type === 'failure') {
            insertItem(this.agents, agent, itemRemoved.index);
        }
    }
}
