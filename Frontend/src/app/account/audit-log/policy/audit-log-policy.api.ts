import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export type AuditLogEventScope = 'application' | 'workspace';

export type AuditLogSeverity = 'verbose' | 'info' | 'warning' | 'critical';

export interface AuditLogEventCatalogEntry {
    eventType: string;
    category: string;
    severity: AuditLogSeverity | string;
    description: string;
    scope: AuditLogEventScope;
}

export interface AuditLogEventCatalog {
    events: AuditLogEventCatalogEntry[];
}

export interface AuditLogPolicy {
    disabledEventTypes: string[];
    /** Sparse: event-type → severity. Missing keys use the natural severity from the catalog. */
    severityOverrides?: Record<string, string> | null;
}

/** GET /workspaces/{id} response — policy + workspace display metadata. The metadata lets the
 *  editor render "Audit log — Acme Marketing" without an extra workspace-info round-trip. */
export interface GetWorkspacePolicyResponse extends AuditLogPolicy {
    workspaceExternalId: string;
    workspaceName: string;
}

export interface AuditLogVolumeStats {
    daysWindow: number;
    workspaceExternalId: string | null;
    countsByEventType: Record<string, number>;
}

export interface AuditLogPolicyWorkspaceItem {
    externalId: string;
    name: string;
    ownerExternalId: string;
    ownerEmail: string;
    disabledCount: number;
    severityOverrideCount: number;
}

export interface AuditLogPolicyWorkspaces {
    workspaces: AuditLogPolicyWorkspaceItem[];
}

@Injectable({
    providedIn: 'root'
})
export class AuditLogPolicyApi {
    constructor(private _http: HttpClient) {}

    getCatalog(): Promise<AuditLogEventCatalog> {
        return firstValueFrom(this._http.get<AuditLogEventCatalog>('/api/audit-log/policy/catalog'));
    }

    getVolumeStats(opts: { workspaceExternalId?: string | null; days?: number }): Promise<AuditLogVolumeStats> {
        let params = new HttpParams();
        if (opts.workspaceExternalId) params = params.set('workspaceExternalId', opts.workspaceExternalId);
        if (opts.days) params = params.set('days', opts.days.toString());
        return firstValueFrom(this._http.get<AuditLogVolumeStats>('/api/audit-log/policy/volume-stats', { params }));
    }

    getAppPolicy(): Promise<AuditLogPolicy> {
        return firstValueFrom(this._http.get<AuditLogPolicy>('/api/audit-log/policy/app'));
    }

    setAppPolicy(policy: AuditLogPolicy): Promise<void> {
        return firstValueFrom(this._http.put<void>('/api/audit-log/policy/app', policy));
    }

    getWorkspaceDefaultPolicy(): Promise<AuditLogPolicy> {
        return firstValueFrom(this._http.get<AuditLogPolicy>('/api/audit-log/policy/workspace-defaults'));
    }

    setWorkspaceDefaultPolicy(policy: AuditLogPolicy): Promise<void> {
        return firstValueFrom(this._http.put<void>('/api/audit-log/policy/workspace-defaults', policy));
    }

    getWorkspacePolicy(workspaceExternalId: string): Promise<GetWorkspacePolicyResponse> {
        return firstValueFrom(this._http.get<GetWorkspacePolicyResponse>(`/api/audit-log/policy/workspaces/${workspaceExternalId}`));
    }

    setWorkspacePolicy(workspaceExternalId: string, policy: AuditLogPolicy): Promise<void> {
        return firstValueFrom(this._http.put<void>(`/api/audit-log/policy/workspaces/${workspaceExternalId}`, policy));
    }

    listWorkspaces(): Promise<AuditLogPolicyWorkspaces> {
        return firstValueFrom(this._http.get<AuditLogPolicyWorkspaces>('/api/audit-log/policy/workspaces'));
    }
}
