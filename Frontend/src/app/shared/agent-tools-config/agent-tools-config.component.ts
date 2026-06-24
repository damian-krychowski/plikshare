import { Component, OnInit, computed, input, signal } from "@angular/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AgentsApi, AgentToolConfig } from "../../services/agents.api";

type ToolGroupKey = 'instance' | 'workspace' | 'box';

interface ToolGroupView {
    key: ToolGroupKey;
    title: string;
    description: string;
    tools: AgentToolConfig[];
    total: number;
    enabledCount: number;
    approvalCount: number;
    disabledCount: number;
}

const GROUP_META: { key: ToolGroupKey; title: string; description: string }[] = [
    {
        key: 'instance',
        title: 'Instance tools',
        description: 'Global tools that are not bound to any single workspace or box. They let the agent discover existing workspaces and boxes, and create new workspaces.'
    },
    {
        key: 'workspace',
        title: 'Workspace tools',
        description: 'Act inside a workspace the agent is a member of - browsing, file operations and share-link management. These defaults apply to every workspace unless overridden per workspace.'
    },
    {
        key: 'box',
        title: 'Box tools',
        description: 'Act on a single box the agent was granted access to, scoped to that box\'s folder. These defaults apply to every box unless overridden per box.'
    }
];

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
    collapsedGroups = signal<Set<ToolGroupKey>>(new Set());

    groups = computed<ToolGroupView[]>(() => {
        const all = this.globalTools();

        return GROUP_META.map(meta => {
            const tools = all.filter(t => t.scope === meta.key);

            return {
                ...meta,
                tools,
                total: tools.length,
                enabledCount: tools.filter(t => t.isEnabled).length,
                approvalCount: tools.filter(t => t.isEnabled && t.requiresApproval).length,
                disabledCount: tools.filter(t => !t.isEnabled).length
            };
        });
    });

    constructor(private _agentsApi: AgentsApi) {}

    async ngOnInit() {
        await this.loadGlobal({ showLoading: true });
    }

    isGroupCollapsed(key: ToolGroupKey) {
        return this.collapsedGroups().has(key);
    }

    toggleGroup(key: ToolGroupKey) {
        this.collapsedGroups.update(current => {
            const next = new Set(current);

            if (next.has(key))
                next.delete(key);
            else
                next.add(key);

            return next;
        });
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
