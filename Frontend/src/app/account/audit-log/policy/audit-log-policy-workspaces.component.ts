import { Component, OnInit, computed, signal } from "@angular/core";
import { Router } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../../services/auth.service";
import { getNameWithHighlight } from "../../../shared/name-with-highlight";
import {
    AuditLogPolicyApi,
    AuditLogPolicyWorkspaceItem
} from "./audit-log-policy.api";

type ModeTab = 'app' | 'workspace-defaults' | 'workspaces';

@Component({
    selector: 'app-audit-log-policy-workspaces',
    imports: [
        FormsModule,
        MatButtonModule,
        MatFormFieldModule,
        MatInputModule,
        MatTooltipModule
    ],
    templateUrl: './audit-log-policy-workspaces.component.html',
    styleUrl: './audit-log-policy-workspaces.component.scss'
})
export class AuditLogPolicyWorkspacesComponent implements OnInit {
    isLoading = signal(false);
    workspaces = signal<AuditLogPolicyWorkspaceItem[]>([]);
    searchQuery = signal('');

    /** Workspaces with at least one disabled event or severity override — what admin most likely
     *  wants to revisit. Sorted to the top of the list; the rest follow in alphabetical order. */
    customizedCount = computed(() =>
        this.workspaces().filter(w => this.isCustomized(w)).length);

    filteredWorkspaces = computed(() => {
        const query = this.searchQuery().toLowerCase().trim();

        const matching = query
            ? this.workspaces().filter(w =>
                w.name.toLowerCase().includes(query)
                || w.ownerEmail.toLowerCase().includes(query))
            : [...this.workspaces()];

        // Stable two-bucket sort: customized first, defaults second; within each bucket the SQL
        // already ordered by name COLLATE NOCASE so we just preserve that.
        matching.sort((a, b) => {
            const ca = this.isCustomized(a) ? 0 : 1;
            const cb = this.isCustomized(b) ? 0 : 1;
            return ca - cb;
        });

        return matching;
    });

    constructor(
        public auth: AuthService,
        private _router: Router,
        private _api: AuditLogPolicyApi
    ) {}

    async ngOnInit() {
        await this.auth.initiateSessionIfNeeded();
        await this.load();
    }

    private async load() {
        this.isLoading.set(true);
        try {
            const result = await this._api.listWorkspaces();
            this.workspaces.set(result.workspaces);
        } catch (err) {
            console.error('Failed to load workspaces list', err);
        } finally {
            this.isLoading.set(false);
        }
    }

    isCustomized(w: AuditLogPolicyWorkspaceItem): boolean {
        return w.disabledCount > 0 || w.severityOverrideCount > 0;
    }

    summaryFor(w: AuditLogPolicyWorkspaceItem): string {
        if (!this.isCustomized(w)) return 'defaults';

        const parts: string[] = [];
        if (w.disabledCount > 0) {
            parts.push(`${w.disabledCount} disabled`);
        }
        if (w.severityOverrideCount > 0) {
            parts.push(`${w.severityOverrideCount} severity ${w.severityOverrideCount === 1 ? 'override' : 'overrides'}`);
        }
        return parts.join(' · ');
    }

    highlightMatch(text: string): string {
        return getNameWithHighlight(text, this.searchQuery().toLowerCase().trim());
    }

    open(externalId: string) {
        this._router.navigate(['settings/audit-log/policy/workspaces', externalId]);
    }

    switchMode(target: ModeTab) {
        if (target === 'workspaces') return;
        if (target === 'app') this._router.navigate(['settings/audit-log/policy/app']);
        if (target === 'workspace-defaults') this._router.navigate(['settings/audit-log/policy/workspace-defaults']);
    }

    goBack() {
        this._router.navigate(['settings/audit-log']);
    }
}
