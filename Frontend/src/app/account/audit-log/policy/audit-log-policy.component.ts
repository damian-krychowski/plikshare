import { Component, OnInit, computed, signal } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCheckboxChange, MatCheckboxModule } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatMenuModule } from "@angular/material/menu";
import { MatTooltipModule } from "@angular/material/tooltip";
import { firstValueFrom } from "rxjs";
import { AuthService } from "../../../services/auth.service";
import { ActionTextButtonComponent } from "../../../shared/buttons/action-text-btn/action-text-btn.component";
import { GenericDialogService } from "../../../shared/generic-message-dialog/generic-dialog-service";
import { getNameWithHighlight } from "../../../shared/name-with-highlight";
import {
    AuditLogEventCatalogEntry,
    AuditLogPolicyApi,
    AuditLogSeverity,
    AuditLogVolumeStats
} from "./audit-log-policy.api";

type PolicyMode = 'app' | 'workspace-defaults' | 'workspace';
type FilterMode = 'all' | 'enabled' | 'disabled';
type BulkState = 'all-enabled' | 'mixed' | 'all-disabled';

const SEVERITIES: AuditLogSeverity[] = ['verbose', 'info', 'warning', 'critical'];

interface CategoryGroup {
    name: string;
    events: AuditLogEventCatalogEntry[];
    enabledCount: number;
    disabledCount: number;
    total: number;
    bulkState: BulkState;
    volumeTotal: number;
    isExpanded: boolean;
}

@Component({
    selector: 'app-audit-log-policy',
    imports: [
        FormsModule,
        MatButtonModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatInputModule,
        MatMenuModule,
        MatTooltipModule,
        ActionTextButtonComponent
    ],
    templateUrl: './audit-log-policy.component.html',
    styleUrl: './audit-log-policy.component.scss'
})
export class AuditLogPolicyComponent implements OnInit {
    mode = signal<PolicyMode>('app');
    workspaceExternalId = signal<string | null>(null);
    workspaceName = signal<string | null>(null);

    isLoading = signal(false);
    isSaving = signal(false);

    catalog = signal<AuditLogEventCatalogEntry[]>([]);
    volumeStats = signal<AuditLogVolumeStats | null>(null);

    // Server-side persisted state — used to compute "isDirty".
    originalDisabled = signal<Set<string>>(new Set<string>());
    originalSeverities = signal<Map<string, string>>(new Map<string, string>());
    // Local edits — toggled by checkbox interactions, persisted on Save.
    draftDisabled = signal<Set<string>>(new Set<string>());
    draftSeverities = signal<Map<string, string>>(new Map<string, string>());

    searchQuery = signal('');
    filterMode = signal<FilterMode>('all');
    expandedCategories = signal<Set<string>>(new Set<string>());

    readonly severities = SEVERITIES;

    /** Map of event-type → natural severity (from the catalog). Used to render the badge with
     *  the effective value and to clear out overrides that match the natural value (sparse). */
    naturalSeverityByEventType = computed(() => {
        const map = new Map<string, string>();
        for (const e of this.catalog()) map.set(e.eventType, e.severity);
        return map;
    });

    title = computed(() => {
        switch (this.mode()) {
            case 'app': return 'Audit log — application events';
            case 'workspace-defaults': return 'Audit log — defaults for new workspaces';
            case 'workspace':
                // Falls back to the external id only on the brief window before loadAll() resolves
                // or when the workspace name couldn't be fetched (unlikely, but harmless).
                return `Audit log — ${this.workspaceName() ?? this.workspaceExternalId() ?? ''}`;
        }
    });

    /** Shown as a small grey line under the title in workspace mode — admin keeps the external id
     *  visible without it dominating the header. Null in other modes. */
    workspaceSubtitleId = computed(() => {
        if (this.mode() !== 'workspace') return null;
        if (!this.workspaceName()) return null; // name not loaded yet → external id already in title
        return this.workspaceExternalId();
    });

    subtitle = computed(() => {
        switch (this.mode()) {
            case 'app':
                return 'These events are logged across the whole application. Disabling an event stops it from being persisted; existing entries are unaffected.';
            case 'workspace-defaults':
                return 'Template applied when a new workspace is created. Editing this does not retroactively change existing workspaces — edit them individually.';
            case 'workspace':
                return 'Live policy for this workspace. Changes take effect immediately.';
        }
    });

    isDirty = computed(() => {
        const a = this.originalDisabled();
        const b = this.draftDisabled();
        if (a.size !== b.size) return true;
        for (const x of a) if (!b.has(x)) return true;

        const sa = this.originalSeverities();
        const sb = this.draftSeverities();
        if (sa.size !== sb.size) return true;
        for (const [k, v] of sa) if (sb.get(k) !== v) return true;
        return false;
    });

    changesCount = computed(() => {
        const a = this.originalDisabled();
        const b = this.draftDisabled();
        let diff = 0;
        for (const x of a) if (!b.has(x)) diff++;
        for (const x of b) if (!a.has(x)) diff++;

        const sa = this.originalSeverities();
        const sb = this.draftSeverities();
        const keys = new Set<string>([...sa.keys(), ...sb.keys()]);
        for (const k of keys) if (sa.get(k) !== sb.get(k)) diff++;

        return diff;
    });

    // Catalog filtered by search and enabled/disabled filter.
    filteredCatalog = computed(() => {
        const query = this.searchQuery().toLowerCase().trim();
        const filter = this.filterMode();
        const disabled = this.draftDisabled();

        return this.catalog().filter(e => {
            if (filter === 'enabled' && disabled.has(e.eventType)) return false;
            if (filter === 'disabled' && !disabled.has(e.eventType)) return false;

            if (query) {
                const hay = (e.eventType + ' ' + e.category + ' ' + e.description).toLowerCase();
                if (!hay.includes(query)) return false;
            }

            return true;
        });
    });

    categoryGroups = computed<CategoryGroup[]>(() => {
        const disabled = this.draftDisabled();
        const volumes = this.volumeStats()?.countsByEventType ?? {};
        const expanded = this.expandedCategories();
        const filtered = this.filteredCatalog();

        const groups = new Map<string, AuditLogEventCatalogEntry[]>();
        for (const e of filtered) {
            const arr = groups.get(e.category);
            if (arr) arr.push(e);
            else groups.set(e.category, [e]);
        }

        const result: CategoryGroup[] = [];
        for (const [name, events] of groups) {
            let enabledCount = 0;
            let disabledCount = 0;
            let volumeTotal = 0;
            for (const e of events) {
                if (disabled.has(e.eventType)) disabledCount++;
                else enabledCount++;
                volumeTotal += volumes[e.eventType] ?? 0;
            }

            const bulkState: BulkState =
                enabledCount === events.length ? 'all-enabled'
                : disabledCount === events.length ? 'all-disabled'
                : 'mixed';

            // Sort events: by severity (critical > warning > info), then by volume desc, then name.
            events.sort((a, b) => {
                const sevDiff = severityWeight(b.severity) - severityWeight(a.severity);
                if (sevDiff !== 0) return sevDiff;
                const vol = (volumes[b.eventType] ?? 0) - (volumes[a.eventType] ?? 0);
                if (vol !== 0) return vol;
                return a.eventType.localeCompare(b.eventType);
            });

            result.push({
                name,
                events,
                enabledCount,
                disabledCount,
                total: events.length,
                bulkState,
                volumeTotal,
                isExpanded: expanded.has(name) || this.searchQuery().trim().length > 0
            });
        }

        result.sort((a, b) => a.name.localeCompare(b.name));
        return result;
    });

    summary = computed(() => {
        const total = this.catalog().length;
        const disabledCount = this.draftDisabled().size;
        return {
            total,
            enabled: total - disabledCount,
            disabled: disabledCount
        };
    });

    constructor(
        public auth: AuthService,
        private _router: Router,
        private _route: ActivatedRoute,
        private _api: AuditLogPolicyApi,
        private _genericDialogService: GenericDialogService
    ) {}

    async ngOnInit() {
        await this.auth.initiateSessionIfNeeded();

        const data = this._route.snapshot.data;
        const mode = (data['mode'] as PolicyMode) ?? 'app';
        this.mode.set(mode);

        if (mode === 'workspace') {
            const externalId = this._route.snapshot.paramMap.get('workspaceExternalId');
            if (!externalId) {
                this._router.navigate(['settings/audit-log']);
                return;
            }
            this.workspaceExternalId.set(externalId);
        }

        await this.loadAll();
    }

    private async loadAll() {
        this.isLoading.set(true);
        try {
            const [catalog, policy, volumes] = await Promise.all([
                this._api.getCatalog(),
                this.loadPolicyForMode(),
                this._api.getVolumeStats({
                    workspaceExternalId: this.mode() === 'workspace' ? this.workspaceExternalId() : null,
                    days: 30
                })
            ]);

            const filteredCatalog = this.filterCatalogByMode(catalog.events);
            this.catalog.set(filteredCatalog);
            this.volumeStats.set(volumes);

            const initialDisabled = new Set(policy.disabledEventTypes);
            this.originalDisabled.set(initialDisabled);
            this.draftDisabled.set(new Set(initialDisabled));

            const initialSeverities = new Map<string, string>(
                Object.entries(policy.severityOverrides ?? {}));
            this.originalSeverities.set(initialSeverities);
            this.draftSeverities.set(new Map(initialSeverities));

            // Only the workspace-mode response carries the workspace name; the other two policy
            // shapes don't have it and that's fine — their titles are static.
            if (this.mode() === 'workspace' && 'workspaceName' in policy) {
                this.workspaceName.set((policy as { workspaceName: string }).workspaceName);
            }
        } catch (err) {
            console.error('Failed to load audit-log policy', err);
        } finally {
            this.isLoading.set(false);
        }
    }

    private filterCatalogByMode(events: AuditLogEventCatalogEntry[]) {
        // App mode shows application-scoped events; both workspace modes show workspace-scoped.
        const targetScope = this.mode() === 'app' ? 'application' : 'workspace';
        return events.filter(e => e.scope === targetScope);
    }

    private loadPolicyForMode() {
        switch (this.mode()) {
            case 'app': return this._api.getAppPolicy();
            case 'workspace-defaults': return this._api.getWorkspaceDefaultPolicy();
            case 'workspace': return this._api.getWorkspacePolicy(this.workspaceExternalId()!);
        }
    }

    isEnabled(eventType: string) {
        return !this.draftDisabled().has(eventType);
    }

    toggleEvent(eventType: string) {
        this.draftDisabled.update(prev => {
            const next = new Set(prev);
            if (next.has(eventType)) next.delete(eventType);
            else next.add(eventType);
            return next;
        });
    }

    bulkToggleCategory(event: MatCheckboxChange, group: CategoryGroup) {
        this.draftDisabled.update(prev => {
            const next = new Set(prev);
            for (const e of group.events) {
                if (event.checked) next.delete(e.eventType);
                else next.add(e.eventType);
            }
            return next;
        });
    }

    toggleExpand(name: string) {
        this.expandedCategories.update(prev => {
            const next = new Set(prev);
            if (next.has(name)) next.delete(name);
            else next.add(name);
            return next;
        });
    }

    expandAll() {
        const all = new Set(this.categoryGroups().map(g => g.name));
        this.expandedCategories.set(all);
    }

    collapseAll() {
        this.expandedCategories.set(new Set());
    }

    setSearch(value: string) {
        this.searchQuery.set(value);
    }

    setFilterMode(mode: FilterMode) {
        this.filterMode.set(mode);
    }

    async save() {
        if (!this.isDirty()) return;

        this.isSaving.set(true);
        try {
            const severityOverrides: Record<string, string> = {};
            for (const [k, v] of this.draftSeverities()) severityOverrides[k] = v;

            const policy = {
                disabledEventTypes: [...this.draftDisabled()].sort(),
                severityOverrides: Object.keys(severityOverrides).length === 0 ? null : severityOverrides
            };

            await this.savePolicyForMode(policy);
            this.originalDisabled.set(new Set(this.draftDisabled()));
            this.originalSeverities.set(new Map(this.draftSeverities()));
        } catch (err) {
            console.error('Failed to save audit-log policy', err);
        } finally {
            this.isSaving.set(false);
        }
    }

    private savePolicyForMode(policy: { disabledEventTypes: string[]; severityOverrides: Record<string, string> | null }) {
        switch (this.mode()) {
            case 'app': return this._api.setAppPolicy(policy);
            case 'workspace-defaults': return this._api.setWorkspaceDefaultPolicy(policy);
            case 'workspace': return this._api.setWorkspacePolicy(this.workspaceExternalId()!, policy);
        }
    }

    discard() {
        this.draftDisabled.set(new Set(this.originalDisabled()));
        this.draftSeverities.set(new Map(this.originalSeverities()));
    }

    /** Effective severity for an event — override if set in draft, otherwise the catalog's natural value. */
    effectiveSeverity(eventType: string): string {
        return this.draftSeverities().get(eventType)
            ?? this.naturalSeverityByEventType().get(eventType)
            ?? 'info';
    }

    /** Apply a severity choice from the dropdown. Sparse: if the choice equals the catalog's
     *  natural value, drop the override entirely so storage stays minimal. */
    setSeverity(eventType: string, severity: AuditLogSeverity) {
        const natural = this.naturalSeverityByEventType().get(eventType);

        this.draftSeverities.update(prev => {
            const next = new Map(prev);
            if (severity === natural) next.delete(eventType);
            else next.set(eventType, severity);
            return next;
        });
    }

    /** True when this event's draft severity differs from what the server returned at load.
     *  Drives the per-event undo button. Compares both "has override?" and override value. */
    hasSeverityChanged(eventType: string): boolean {
        return this.draftSeverities().get(eventType) !== this.originalSeverities().get(eventType);
    }

    /** Revert this single event's severity back to what was loaded from the server, without
     *  touching other events. Handy when admin made one wrong click in a long edit session. */
    undoSeverity(eventType: string) {
        const original = this.originalSeverities().get(eventType);

        this.draftSeverities.update(prev => {
            const next = new Map(prev);
            if (original === undefined) next.delete(eventType);
            else next.set(eventType, original);
            return next;
        });
    }

    volumeFor(eventType: string): number {
        return this.volumeStats()?.countsByEventType[eventType] ?? 0;
    }

    formatVolume(count: number): string {
        if (count === 0) return '0';
        if (count >= 1000) return `${(count / 1000).toFixed(1)}k`;
        return count.toString();
    }

    highlightMatch(text: string): string {
        return getNameWithHighlight(text, this.searchQuery().toLowerCase().trim());
    }

    getSeverityClass(severity: string): string {
        switch (severity) {
            case 'verbose': return 'severity--verbose';
            case 'warning': return 'severity--warning';
            case 'critical': return 'severity--critical';
            default: return 'severity--info';
        }
    }

    goBack() {
        // From a workspace-detail page the natural back step is the workspaces list, not the
        // audit log viewer two levels up.
        if (this.mode() === 'workspace') {
            this._router.navigate(['settings/audit-log/policy/workspaces']);
        } else {
            this._router.navigate(['settings/audit-log']);
        }
    }

    async switchMode(target: 'app' | 'workspace-defaults' | 'workspaces') {
        // Re-clicking the active tab when already on a workspace-detail page is fine — it sends
        // admin back to the workspace list. Same-mode no-op only for the two singleton tabs.
        if ((target === 'app' || target === 'workspace-defaults') && target === this.mode()) return;

        if (this.isDirty()) {
            const confirmed = await firstValueFrom(
                this._genericDialogService.openGenericMessageDialog({
                    title: 'Discard unsaved changes?',
                    message: 'You have unsaved changes to this policy. Switch tabs and discard them?',
                    confirmButtonText: 'Discard',
                    showCancelButton: true,
                    cancelButtonText: 'Keep editing',
                    isDanger: true
                }));
            if (!confirmed) return;
        }

        if (target === 'app') {
            this._router.navigate(['settings/audit-log/policy/app']);
        } else if (target === 'workspace-defaults') {
            this._router.navigate(['settings/audit-log/policy/workspace-defaults']);
        } else if (target === 'workspaces') {
            this._router.navigate(['settings/audit-log/policy/workspaces']);
        }
    }
}

function severityWeight(severity: string): number {
    switch (severity) {
        case 'critical': return 2;
        case 'warning': return 1;
        default: return 0;
    }
}
