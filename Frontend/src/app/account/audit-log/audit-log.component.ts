import { Component, OnInit, signal, computed } from "@angular/core";
import { Router } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { DateAdapter, MAT_DATE_FORMATS, MAT_DATE_LOCALE, MAT_NATIVE_DATE_FORMATS } from "@angular/material/core";
import { IsoDateAdapter } from "./iso-date-adapter";
import { AuthService } from "../../services/auth.service";
import { AuditLogApi, AuditLogItem, AuditLogStats, AuditLogFilters } from "./audit-log.api";
import { DatePipe } from "@angular/common";
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { ActionTextButtonComponent } from "../../shared/buttons/action-text-btn/action-text-btn.component";
import { ConfirmOperationDirective } from "../../shared/operation-confirm/confirm-operation.directive";

@Component({
    selector: 'app-audit-log',
    imports: [
        FormsModule,
        MatButtonModule,
        MatTooltipModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatDatepickerModule,
        DatePipe,
        ActionButtonComponent,
        ActionTextButtonComponent,
        ConfirmOperationDirective
    ],
    providers: [
        { provide: DateAdapter, useClass: IsoDateAdapter },
        { provide: MAT_DATE_FORMATS, useValue: MAT_NATIVE_DATE_FORMATS },
    ],
    templateUrl: './audit-log.component.html',
    styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
    isLoading = signal(false);
    isLoadingMore = signal(false);
    items = signal<AuditLogItem[]>([]);
    nextCursor = signal<number | null>(null);
    hasMore = signal(false);
    stats = signal<AuditLogStats | null>(null);
    expandedExternalId = signal<string | null>(null);
    showFilters = signal(false);

    // Filters
    filterFromDate = signal<Date | null>(null);
    filterToDate = signal<Date | null>(null);
    filterEventCategories = signal<string[]>([]);
    filterEventTypes = signal<string[]>([]);
    filterSeverities = signal<string[]>([]);
    filterActorIdentities = signal<string[]>([]);
    filterCorrelationId = signal('');
    filterSearch = signal('');

    // Filter options (from API)
    availableEventTypes = signal<string[]>([]);
    availableActors = signal<string[]>([]);

    // Management
    deleteBeforeDate = signal<Date | null>(null);
    archiveBeforeDate = signal<Date | null>(null);
    isDeleting = signal(false);
    isArchiving = signal(false);
    lastDeleteResult = signal<string | null>(null);
    lastArchiveResult = signal<string | null>(null);

    dbSizeFormatted = computed(() => {
        const s = this.stats();
        if (!s) return '---';
        return this.formatBytes(s.dbSizeInBytes);
    });

    eventCategories = [
        'auth', 'user', 'workspace', 'file', 'folder',
        'box', 'box-link', 'storage', 'integration',
        'email-provider', 'auth-provider', 'settings'
    ];

    severities = ['info', 'warning', 'critical'];

    constructor(
        public auth: AuthService,
        private _router: Router,
        private _auditLogApi: AuditLogApi
    ) {}

    async ngOnInit() {
        await this.auth.initiateSessionIfNeeded();
        await Promise.all([this.loadLogs(), this.loadStats(), this.loadFilterOptions()]);
    }

    goToAccount() {
        this._router.navigate(['account']);
    }

    private buildFilters(cursor?: number | null): AuditLogFilters {
        return {
            cursor: cursor ?? null,
            pageSize: 50,
            fromDate: this.filterFromDate()?.toISOString() ?? null,
            toDate: this.filterToDate()?.toISOString() ?? null,
            eventCategories: this.filterEventCategories().length > 0 ? this.filterEventCategories() : null,
            eventTypes: this.filterEventTypes().length > 0 ? this.filterEventTypes() : null,
            severities: this.filterSeverities().length > 0 ? this.filterSeverities() : null,
            actorIdentities: this.filterActorIdentities().length > 0 ? this.filterActorIdentities() : null,
            correlationId: this.filterCorrelationId() || null,
            search: this.filterSearch() || null
        };
    }

    async loadLogs() {
        this.isLoading.set(true);
        try {
            const result = await this._auditLogApi.getAuditLog(this.buildFilters());
            this.items.set(result.items);
            this.nextCursor.set(result.nextCursor);
            this.hasMore.set(result.hasMore);
        } catch (error) {
            console.error('Failed to load audit logs', error);
        } finally {
            this.isLoading.set(false);
        }
    }

    async loadMore() {
        if (!this.hasMore() || this.isLoadingMore()) return;
        this.isLoadingMore.set(true);
        try {
            const result = await this._auditLogApi.getAuditLog(this.buildFilters(this.nextCursor()));
            this.items.update(current => [...current, ...result.items]);
            this.nextCursor.set(result.nextCursor);
            this.hasMore.set(result.hasMore);
        } catch (error) {
            console.error('Failed to load more audit logs', error);
        } finally {
            this.isLoadingMore.set(false);
        }
    }

    async loadStats() {
        try {
            const stats = await this._auditLogApi.getStats();
            this.stats.set(stats);
        } catch (error) {
            console.error('Failed to load stats', error);
        }
    }

    async loadFilterOptions() {
        try {
            const options = await this._auditLogApi.getFilterOptions();
            this.availableEventTypes.set(options.eventTypes);
            this.availableActors.set(options.actors);
        } catch (error) {
            console.error('Failed to load filter options', error);
        }
    }

    async applyFilters() {
        await this.loadLogs();
    }

    async clearFilters() {
        this.filterFromDate.set(null);
        this.filterToDate.set(null);
        this.filterEventCategories.set([]);
        this.filterEventTypes.set([]);
        this.filterSeverities.set([]);
        this.filterActorIdentities.set([]);
        this.filterCorrelationId.set('');
        this.filterSearch.set('');
        await this.loadLogs();
    }

    toggleFilters() {
        this.showFilters.update(v => !v);
    }

    async searchByCorrelationId(correlationId: string) {
        this.filterCorrelationId.set(correlationId);
        this.showFilters.set(true);
        await this.loadLogs();
    }

    toggleExpand(externalId: string) {
        this.expandedExternalId.update(current =>
            current === externalId ? null : externalId);
    }

    isExpanded(externalId: string) {
        return this.expandedExternalId() === externalId;
    }

    formatDetails(details: string | null): string {
        if (!details) return '';
        try {
            return JSON.stringify(JSON.parse(details), null, 2);
        } catch {
            return details;
        }
    }

    getSeverityClass(severity: string): string {
        switch (severity) {
            case 'warning': return 'severity--warning';
            case 'critical': return 'severity--critical';
            default: return 'severity--info';
        }
    }

    async deleteOldLogs() {
        const date = this.deleteBeforeDate();
        if (!date) return;

        const dateStr = date.toISOString();

        this.isDeleting.set(true);
        this.lastDeleteResult.set(null);
        try {
            const result = await this._auditLogApi.deleteOldLogs(dateStr);
            this.lastDeleteResult.set(`Deleted ${result.deletedCount} log entries.`);
            await Promise.all([this.loadLogs(), this.loadStats()]);
        } catch (error) {
            console.error('Failed to delete old logs', error);
            this.lastDeleteResult.set('Failed to delete logs.');
        } finally {
            this.isDeleting.set(false);
        }
    }

    async archiveLogs() {
        this.isArchiving.set(true);
        this.lastArchiveResult.set(null);
        try {
            const date = this.archiveBeforeDate()?.toISOString();
            const result = await this._auditLogApi.archiveLogs(date);
            this.lastArchiveResult.set(
                `Archived ${result.archivedCount} entries to ${result.fileName}`);
        } catch (error) {
            console.error('Failed to archive logs', error);
            this.lastArchiveResult.set('Failed to archive logs.');
        } finally {
            this.isArchiving.set(false);
        }
    }

    private formatBytes(bytes: number): string {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }
}
