import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { ProtoHttp } from "../../services/protobuf-http.service";
import { getAuditLogResponseDtoProtobuf } from "../../protobuf/audit-log-response-dto.protobuf";

export interface AuditLogFilters {
    cursor?: number | null;
    pageSize: number;
    fromDate?: string | null;
    toDate?: string | null;
    eventCategories?: string[] | null;
    eventTypes?: string[] | null;
    severities?: string[] | null;
    actorIdentities?: string[] | null;
    correlationId?: string | null;
    workspaceExternalId?: string | null;
    search?: string | null;
}

export interface GetAuditLogResponse {
    items: AuditLogItem[];
    nextCursor: number | null;
    hasMore: boolean;
}

export interface AuditLogItem {
    externalId: string;
    createdAt: string;
    actorEmail: string | null;
    actorIdentity: string;
    eventType: string;
    eventSeverity: string;
}

export interface AuditLogEntryDetails {
    externalId: string;
    createdAt: string;
    correlationId: string | null;
    actorIdentityType: string;
    actorIdentity: string;
    actorEmail: string | null;
    actorIp: string | null;
    eventCategory: string;
    eventType: string;
    eventSeverity: string;
    workspaceExternalId: string | null;
    details: string | null;
}

export interface AuditLogStats {
    dbSizeInBytes: number;
    totalLogCount: number;
    oldestEntryDate: string | null;
    newestEntryDate: string | null;
}

export interface AuditLogFilterOptions {
    eventTypes: string[];
    actors: string[];
}

export interface DeleteOldLogsResponse {
    deletedCount: number;
}

export interface ArchiveLogsResponse {
    fileName: string;
    archivedCount: number;
}

const auditLogResponseProtobuf = getAuditLogResponseDtoProtobuf();

@Injectable({
    providedIn: 'root'
})
export class AuditLogApi {
    constructor(
        private _http: HttpClient,
        private _protoHttp: ProtoHttp
    ) {}

    async getAuditLog(filters: AuditLogFilters): Promise<GetAuditLogResponse> {
        return this._protoHttp.postJsonToProto<AuditLogFilters, GetAuditLogResponse>({
            route: '/api/audit-log',
            request: filters,
            responseProtoType: auditLogResponseProtobuf,
        });
    }

    async getEntryDetails(externalId: string): Promise<AuditLogEntryDetails> {
        const call = this._http.get<AuditLogEntryDetails>(`/api/audit-log/${externalId}`);
        return await firstValueFrom(call);
    }

    async getStats(): Promise<AuditLogStats> {
        const call = this._http.get<AuditLogStats>('/api/audit-log/stats');
        return await firstValueFrom(call);
    }

    async getFilterOptions(): Promise<AuditLogFilterOptions> {
        const call = this._http.get<AuditLogFilterOptions>('/api/audit-log/filter-options');
        return await firstValueFrom(call);
    }

    async deleteOldLogs(olderThanDate: string): Promise<DeleteOldLogsResponse> {
        const call = this._http.post<DeleteOldLogsResponse>('/api/audit-log/delete-old', {
            olderThanDate
        });
        return await firstValueFrom(call);
    }

    async archiveLogs(olderThanDate?: string): Promise<ArchiveLogsResponse> {
        const call = this._http.post<ArchiveLogsResponse>('/api/audit-log/archive', {
            olderThanDate: olderThanDate ?? null
        });
        return await firstValueFrom(call);
    }
}
