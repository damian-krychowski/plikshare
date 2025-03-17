import { Injectable } from "@angular/core";
import { BoxesGetApi, GetBoxListResponse, GetBoxResponse } from "./boxes.api";
import { GetWorkspaceDetailsResponse, GetWorkspaceMembersList, WorkspacesApi } from "./workspaces.api";
import { GetUploadListResponse, UploadsApi } from "./uploads.api";
import { FoldersAndFilesGetApi, GetAiMessagesResponse, GetFolderResponse } from "./folders-and-files.api";
import { DashboardApi, GetDashboardDataResponse } from "./dashboard.api";
import { GetBoxDetailsAndFolderResponse, GetBoxHtmlResponse } from "../external-access/contracts/external-access.contracts";
import { ExternalBoxesGetApi } from "../external-access/external-box/external-boxes.api";
import { AccountApi } from "./account.api";
import { GetUserDetailsResponse, GetUsersResponseDto, UsersApi } from "./users.api";
import { GetStoragesResponse, StoragesApi } from "./storages.api";
import { EmailProvidersApi, GetEmailProvidersResponse } from "./email-providers.api";
import { GetIntegrationsResponse, IntegrationsApi } from "./integrations.api";

type dataPrefetch<T> = {
    promise: Promise<T>;
    wasRead: boolean;
}

@Injectable({
    providedIn: 'root',
})
export class DataStore {
    private _data: Map<string, dataPrefetch<any>> = new Map();

    constructor(
        private _boxesGetApi: BoxesGetApi,
        private _workspacesApi: WorkspacesApi,
        private _uploadsApi: UploadsApi,
        private _foldersAndFilesApi: FoldersAndFilesGetApi,
        private _dashboardApi: DashboardApi,
        private _accountApi: AccountApi,
        private _externalBoxesGetApi: ExternalBoxesGetApi,
        private _usersApi: UsersApi,
        private _storagesApi: StoragesApi,
        private _emailProvidersApi: EmailProvidersApi,
        private _integrationsApi: IntegrationsApi
    ) {

    }

    public clear() {
        this._data = new Map();
    }

    public prefetch<T>(key: string, source: () => Promise<T>): void {
        if(this._data.has(key)) {
            const data = this._data.get(key)!;

            if(!data.wasRead) {
                return;
            }
        }

        const data: dataPrefetch<T> = {
            promise: source(),
            wasRead: false
        };

        this._data.set(key, data);
    }

    public get<T>(key: string, source: () => Promise<T>): Promise<T> {
        const data = this._data.get(key);

        if(data && !data.wasRead) {
            data.wasRead = true;
            return data.promise;
        }

        if(data && data.wasRead) {
            this._data.delete(key);
        }

        this.prefetch<T>(key, source);

        return this.get(key, source);
    }

    public prefetchBoxes(workspaceExternalId: string): void {       
        this.prefetch<GetBoxListResponse>(
            this.boxesKey(workspaceExternalId), 
            () => this._boxesGetApi.getBoxes(workspaceExternalId));
    }

    public getBoxes(workspaceExternalId: string): Promise<GetBoxListResponse> {
        return this.get<GetBoxListResponse>(
            this.boxesKey(workspaceExternalId), 
            () => this._boxesGetApi.getBoxes(workspaceExternalId)); 
    }

    public boxesKey(workspaceExternalId: string): string {
        return `workspaces/${workspaceExternalId}/boxes`;
    }

    public prefetchBox(workspaceExternalId: string, boxExternalId: string | null): void {
        if(!boxExternalId)
            return;

        this.prefetch(
            this.boxKey(workspaceExternalId, boxExternalId),
            () => this._boxesGetApi.getBox(workspaceExternalId, boxExternalId));
    }

    public getBox(workspaceExternalId: string, boxExternalId: string): Promise<GetBoxResponse> {
        return this.get(
            this.boxKey(workspaceExternalId, boxExternalId),
            () => this._boxesGetApi.getBox(workspaceExternalId, boxExternalId));
    }

    public boxKey(workspaceExternalId: string, boxExternalId: string): string {
        return `workspaces/${workspaceExternalId}/boxes/${boxExternalId}`;
    }

    public prefetchWorkspaceMemberList(workspaceExternalId: string): void {
        this.prefetch(
            this.workspaceMemberListKey(workspaceExternalId),
            () => this._workspacesApi.getWorkspaceMemberList(workspaceExternalId));
    }

    public getWorkspaceMemberList(workspaceExternalId: string): Promise<GetWorkspaceMembersList> {
        return this.get(
            this.workspaceMemberListKey(workspaceExternalId),
            () => this._workspacesApi.getWorkspaceMemberList(workspaceExternalId));
    }

    public workspaceMemberListKey(workspaceExternalId: string): string {
        return `workspaces/${workspaceExternalId}/team`;
    }

    public prefetchUploads(workspaceExternalId: string): void {
        this.prefetch(
            this.uploadsKey(workspaceExternalId),
            () => this._uploadsApi.getUploadList(workspaceExternalId));
    }

    public getUploadList(workspaceExternalId: string): Promise<GetUploadListResponse> {
        return this.get(
            this.uploadsKey(workspaceExternalId),
            () => this._uploadsApi.getUploadList(workspaceExternalId));
    }

    public uploadsKey(workspaceExternalId: string): string {
        return `workspaces/${workspaceExternalId}/uploads`;
    }

    public prefetchWorkspace(workspaceExternalId: string): void {
        this.prefetchWorkspaceDetails(workspaceExternalId);
        this.prefetchWorkspaceTopFolders(workspaceExternalId);
    }

    public prefetchWorkspaceDetails(workspaceExternalId: string): void {
        this.prefetch<GetWorkspaceDetailsResponse>(
            this.workspaceDetailsKey(workspaceExternalId),
            () => this._workspacesApi.getWorkspace(workspaceExternalId));
    }

    public getWorkspaceDetails(workspaceExternalId: string): Promise<GetWorkspaceDetailsResponse> {
        return this.get<GetWorkspaceDetailsResponse>(
            this.workspaceDetailsKey(workspaceExternalId),
            () => this._workspacesApi.getWorkspace(workspaceExternalId));
    }

    public clearWorkspaceDetails(workspaceExternalId: string) {
        this._data.delete(this.workspaceDetailsKey(workspaceExternalId));
    }

    public prefetchWorkspaceAiMessages(workspaceExternalId: string, fileExternalId: string, fileArtifactExternalId: string): void {
        this.prefetch<GetAiMessagesResponse>(
            this.workspaceAiMessagesKey(workspaceExternalId, fileExternalId, fileArtifactExternalId),
            () => this._foldersAndFilesApi.getAiMessages({
                workspaceExternalId: workspaceExternalId,
                fileExternalId: fileExternalId,
                fileArtifactExternalId: fileArtifactExternalId,
                fromConversationCounter: 0
            }));
    }

    public getWorkspaceAiMessages(workspaceExternalId: string, fileExternalId: string, fileArtifactExternalId: string): Promise<GetAiMessagesResponse> {
        return this.get<GetAiMessagesResponse>(
            this.workspaceAiMessagesKey(workspaceExternalId, fileExternalId, fileArtifactExternalId),
            () => this._foldersAndFilesApi.getAiMessages({
                workspaceExternalId: workspaceExternalId,
                fileExternalId: fileExternalId,
                fileArtifactExternalId: fileArtifactExternalId,
                fromConversationCounter: 0
            }));
    }

    public workspaceAiMessagesKey(workspaceExternalId: string, fileExternalId: string, fileArtifactExternalId: string) {
        return `workspaces/${workspaceExternalId}/files/${fileExternalId}/conversations/${fileArtifactExternalId}/messages`;
    }

    public prefetchWorkspaceTopFolders(workspaceExternalId: string): void {
        this.prefetch<GetFolderResponse>(
            this.topFolderKey(workspaceExternalId),
            () => this._foldersAndFilesApi.getTopFolders(workspaceExternalId));
    }

    public getWorkspaceTopFolders(workspaceExternalId: string): Promise<GetFolderResponse> {
        return this.get<GetFolderResponse>(
            this.topFolderKey(workspaceExternalId),
            () => this._foldersAndFilesApi.getTopFolders(workspaceExternalId));
    }

    public workspaceDetailsKey(workspaceExternalId: string): string {
        return `workspaces/${workspaceExternalId}/details`;
    }   

    public topFolderKey(workspaceExternalId: string): string {
        return `workspaces/${workspaceExternalId}/folders`;
    }

    public prefetchWorkspaceFolder(workspaceExternalId: string, folderExternalId: string): void {
        this.prefetch(
            this.folderKey(workspaceExternalId, folderExternalId),
            () => this._foldersAndFilesApi.getFolder(workspaceExternalId, folderExternalId));
    }

    public getWorkspaceFolder(workspaceExternalId: string, folderExternalId: string): Promise<GetFolderResponse> {
        return this.get(
            this.folderKey(workspaceExternalId, folderExternalId),
            () => this._foldersAndFilesApi.getFolder(workspaceExternalId, folderExternalId));
    }

    public folderKey(workspaceExternalId: string, folderExternalId: string): string {
        return `workspaces/${workspaceExternalId}/folders/${folderExternalId}`;
    }

    public prefetchUserDetails(userExternalId: string) {
        this.prefetch(
            this.userDetailsKey(userExternalId),
            () => this._usersApi.getUserDetails(userExternalId)
        )
    }

    public getUserDetails(userExternalId: string): Promise<GetUserDetailsResponse> {
        return this.get(
            this.userDetailsKey(userExternalId),
            () => this._usersApi.getUserDetails(userExternalId)
        )
    }

    public prefetchEmailProviders(): void {
        this.prefetch(
            'email-providers',
            () => this._emailProvidersApi.getEmailProviders()
        );
    }

    public getEmailProviders(): Promise<GetEmailProvidersResponse> {
        return this.get(
            'email-providers',
            () => this._emailProvidersApi.getEmailProviders());
    }

    public prefetchIntegrations(): void {
        this.prefetch(
            'integrations',
            () => this._integrationsApi.getIntegrations()
        );
    }

    public getIntegrations(): Promise<GetIntegrationsResponse> {
        return this.get(
            'integrations',
            () => this._integrationsApi.getIntegrations());
    }

    public prefetchStorages(): void {
        this.prefetch(
            'storages',
            () => this._storagesApi.getStorages()
        );
    }

    public getStorages(): Promise<GetStoragesResponse> {
        return this.get(
            'storages',
            () => this._storagesApi.getStorages());
    }

    public prefetchUsers(): void {
        this.prefetch(
            'users',
            () => this._usersApi.getUsers()
        )
    }

    public getUsers(): Promise<GetUsersResponseDto> {
        return this.get(
            'users',
            () => this._usersApi.getUsers());
    }

    public prefetchDashboardData(): void {
        this.prefetch(
            'dashboard',
            () => this._dashboardApi.getDashboardData());
    }

    public getDashboardData(): Promise<GetDashboardDataResponse> {
        return this.get(
            'dashboard',
            () => this._dashboardApi.getDashboardData());
    }

    public clearDashboardData() {
        this._data.delete('dashboard');
    }

    public prefetchExternalBoxDetailsAndContent(boxExternalId: string): void {
        this.prefetch<GetFolderResponse>(
            this.externalBoxDetailsAndContentKey(boxExternalId, null),
            () => this._externalBoxesGetApi.getDetailsAndContent(boxExternalId, null));
    }

    public getExternalBoxDetailsAndContent(boxExternalId: string, folderExternalId: string | null): Promise<GetBoxDetailsAndFolderResponse> {
        return this.get(
            this.externalBoxDetailsAndContentKey(boxExternalId, folderExternalId),
            () => this._externalBoxesGetApi.getDetailsAndContent(boxExternalId, folderExternalId));
    }

    public getExternalBoxHtml(boxExternalId: string): Promise<GetBoxHtmlResponse> {
        return this._externalBoxesGetApi.getHtml(boxExternalId);
    }

    public prefetchExternalBoxFolders(boxExternalId: string): void {
        this.prefetch<GetFolderResponse>(
            this.externalBoxFoldersKey(boxExternalId),
            () => this._externalBoxesGetApi.getContent(boxExternalId, null));
    }

    public getExternalBoxFolders(boxExternalId: string): Promise<GetFolderResponse> {
        return this.get(
            this.externalBoxFoldersKey(boxExternalId),
            () => this._externalBoxesGetApi.getContent(boxExternalId, null));
    }

    public prefetchExternalBoxFolder(boxExternalId: string, folderExternalId: string): void {
        this.prefetch(
            this.externalBoxFolderKey(boxExternalId, folderExternalId),
            () => this._externalBoxesGetApi.getContent(boxExternalId, folderExternalId));
    }

    public getExternalBoxFolder(boxExternalId: string, folderExternalId: string): Promise<GetFolderResponse> {
        return this.get(
            this.externalBoxFolderKey(boxExternalId, folderExternalId),
            () => this._externalBoxesGetApi.getContent(boxExternalId, folderExternalId));
    }

    public externalBoxKeysPrefix(boxExternalId: string): string {
        return `external-boxes/${boxExternalId}`;
    }

    public externalBoxDetailsAndContentKey(boxExternalId: string, folderExternalId: string | null): string {
        return `external-boxes/${boxExternalId}/${folderExternalId ?? ''}`;
    }    

    public externalBoxFoldersKey(boxExternalId: string): string {
        return `external-boxes/${boxExternalId}/folders`;
    }

    public externalBoxFolderKey(boxExternalId: string, folderExternalId: string): string {
        return `external-boxes/${boxExternalId}/folders/${folderExternalId}`;
    }

    public userDetailsKey(userExternalId: string) {
        return `users/${userExternalId}`;
    }

    public invalidateEntries(filter: (key: string) => boolean): void {
        for(const key of this._data.keys()) {
            if(filter(key)) {
                this._data.delete(key);
            }
        }
    }
}  