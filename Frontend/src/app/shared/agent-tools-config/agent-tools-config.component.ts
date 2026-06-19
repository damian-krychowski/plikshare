import { Component, OnInit, computed, input, signal } from "@angular/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AgentsApi, AgentToolConfig } from "../../services/agents.api";

@Component({
    selector: 'app-agent-tools-config',
    standalone: true,
    imports: [
        MatSlideToggleModule,
        MatTooltipModule
    ],
    templateUrl: './agent-tools-config.component.html',
    styleUrl: './agent-tools-config.component.scss'
})
export class AgentToolsConfigComponent implements OnInit {
    agentExternalId = input.required<string>();

    isLoading = signal(false);
    globalTools = signal<AgentToolConfig[]>([]);

    instanceTools = computed(() => this.globalTools().filter(t => t.scope === 'instance'));
    workspaceGlobalTools = computed(() => this.globalTools().filter(t => t.scope === 'workspace'));

    constructor(private _agentsApi: AgentsApi) {}

    async ngOnInit() {
        await this.loadGlobal({ showLoading: true });
    }

    // showLoading only on the initial fetch. The post-save refresh must stay silent: flipping
    // isLoading tears down and rebuilds the whole list (the template gates it behind @if(isLoading)),
    // which is the flicker on every toggle. The optimistic patch already updated the row in place;
    // this refresh just reconciles the value-based isDefault flag without re-rendering everything.
    private async loadGlobal(options: { showLoading: boolean } = { showLoading: false }) {
        if (options.showLoading)
            this.isLoading.set(true);

        try {
            const result = await this._agentsApi.getAgentTools(this.agentExternalId());
            this.globalTools.set(result.tools);
        } catch (error) {
            console.error(error);
        } finally {
            if (options.showLoading)
                this.isLoading.set(false);
        }
    }

    async onToggleGlobalEnabled(tool: AgentToolConfig) {
        await this.saveGlobal(tool, !tool.isEnabled, tool.requiresApproval);
    }

    async onToggleGlobalApproval(tool: AgentToolConfig) {
        await this.saveGlobal(tool, tool.isEnabled, !tool.requiresApproval);
    }

    private async saveGlobal(tool: AgentToolConfig, isEnabled: boolean, requiresApproval: boolean) {
        // Optimistic toggle for snappy UI; reload to get the value-based isDefault from the backend.
        this.patchGlobalLocal(tool.name, { isEnabled, requiresApproval });

        try {
            await this._agentsApi.updateAgentToolConfig(this.agentExternalId(), tool.name, {
                isEnabled,
                requiresApproval
            });
        } catch (error) {
            console.error(error);
        }

        await this.loadGlobal();
    }

    private patchGlobalLocal(name: string, patch: Partial<AgentToolConfig>) {
        this.globalTools.update(tools =>
            tools.map(tool => tool.name === name ? { ...tool, ...patch } : tool));
    }
}
