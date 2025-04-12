import { Component, WritableSignal, computed, input, output, signal } from "@angular/core";
import { Router, RouterModule } from "@angular/router";
import { ConfirmOperationDirective } from "../../../../shared/operation-confirm/confirm-operation.directive";
import { EditableTxtComponent } from "../../../../shared/editable-txt/editable-txt.component";
import { AppBoxPermissions, BoxPermissionsListComponent, mapPermissionsToDto } from "../../../../shared/box-permissions/box-permissions-list.component";
import { BoxLinksApi } from "../../../../services/box-links.api";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { toggle } from "../../../../shared/signal-utils";
import { ClipboardModule, Clipboard } from '@angular/cdk/clipboard';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActionButtonComponent } from "../../../../shared/buttons/action-btn/action-btn.component";import { MatTooltipModule } from "@angular/material/tooltip";
import { MatDialog } from "@angular/material/dialog";
import { BoxWidgetComponent } from "../../../../external-access/box-widget/box-widget.component";
import { BoxWidgetSetupComponent } from "../box-widget-setup/box-widget-setup.component";
;

export type AppBoxLink = {
    externalId: WritableSignal<string | null>;
    accessCode: WritableSignal<string | null>;
    workspaceExternalId: string;

    name: WritableSignal<string>;
    isEnabled: WritableSignal<boolean>;
    widgetOrigins: WritableSignal<string[]>;

    permissions: AppBoxPermissions;

    isNameEditing: WritableSignal<boolean>;
}

@Component({
    selector: 'app-box-link-item',
    imports: [
        RouterModule,
        MatSlideToggleModule,
        EditableTxtComponent,
        ConfirmOperationDirective,
        BoxPermissionsListComponent,
        ClipboardModule,
        ActionButtonComponent,
        MatTooltipModule
    ],
    templateUrl: './box-link-item.component.html',
    styleUrl: './box-link-item.component.scss'
})
export class BoxLinkItemComponent {

    link = input.required<AppBoxLink>();

    deleted = output<void>();

    name = computed(() => this.link().name());
    nameToDisplay = computed(() => this.name() + (this.isEnabled() ? '' : ' (disabled)'))
    url = computed(() => this.getLinkUrl(this.link().accessCode()));
    permissions = computed(() => this.link().permissions);

    isEnabled = computed(() => this.link().isEnabled());
    isNameEditing = computed(() => this.link().isNameEditing());
    widgetOrigins = computed((() => this.link().widgetOrigins()));

    areActionsVisible = signal(false);

    constructor(
        private _router: Router,
        private _boxLinksApi: BoxLinksApi,
        private _clipboard: Clipboard,
        private _snackBar: MatSnackBar,
        private _dialog: MatDialog
    ) {

    }

    async saveLinkName(newName: string) {
        const link = this.link();
        const externalId = link.externalId();

        if (!externalId)
            return;

        link.name.set(newName);

        await this._boxLinksApi.updateBoxLinkName(link.workspaceExternalId, externalId, {
            name: newName
        });
    }

    async changeLinkIsEnabled() {
        const link = this.link();
        const externalId = link.externalId();

        if (!externalId)
            return;

        const isEnabled = toggle(link.isEnabled);

        await this._boxLinksApi.updateBoxLinkIsEnabled(link.workspaceExternalId, externalId, {
            isEnabled: isEnabled
        });
    }

    async regenerateLinkAccessCode() {
        const link = this.link();
        const externalId = link.externalId();

        if (!externalId)
            return;

        const result = await this
            ._boxLinksApi
            .regenerateAccessCode(link.workspaceExternalId, externalId);

        link.accessCode.set(result.accessCode);
    }

    async openLink() {
        const link = this.link();
        const accessCode = link.accessCode();

        if (!accessCode)
            return;

        this._router.navigate([`link/${accessCode}`]);
    }

    editName() {
        this.link().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    async deleteLink() {
        const link = this.link();
        const externalId = link.externalId();

        if (!externalId)
            return;

        this.deleted.emit();

        await this._boxLinksApi.deleteBoxLink(
            link.workspaceExternalId,
            externalId);
    }

    async changeLinkPermissions() {
        const link = this.link();
        const externalId = link.externalId();

        if (!externalId)
            return;

        await this._boxLinksApi.updateBoxLinkPermissions(
            link.workspaceExternalId,
            externalId,
            mapPermissionsToDto(link.permissions));
    }

    private getLinkUrl(accessCode: string | null): string {
        if (!accessCode)
            return '';

        return `${this.getAppUrl()}/link/${accessCode}`;
    }

    private getAppUrl() {
        const protocol = window.location.protocol;
        const hostname = window.location.hostname;
        const port = window.location.port ? `:${window.location.port}` : '';
        return `${protocol}//${hostname}${port}`;
    }

    toggleActions() {
        this.areActionsVisible.set(!this.areActionsVisible());
    }

    copyToClipboard() {
        if(this._clipboard.copy(this.url())) {
            const iconElement = document.querySelector('.url .icon-nucleo-copy');
            
            if (iconElement) {
                iconElement.classList.add('copy-animation');
                setTimeout(() => {
                    iconElement.classList.remove('copy-animation');
                }, 300);
            }

            this._snackBar.open('Link copied to clipboard', 'Close', {
                duration: 2000,
            });
        }
    }

    openWidgetPopup() {
        const dialogRef = this._dialog.open(BoxWidgetSetupComponent, {
            width: '900px',
            maxHeight: '80vh',
            position: {
                top: '100px'
            },
            data: {
                url: this.url(),
                origins: this.widgetOrigins()
            }
        });
        
        dialogRef.afterClosed().subscribe(async (widgetOrigins: string[] | undefined) => {
            if(!widgetOrigins)
                return;

            const link = this.link();
            const externalId = link.externalId();

            if (!externalId)
                return;

            link.widgetOrigins.set(widgetOrigins);

            await this._boxLinksApi.updateBoxLinkWidgetOrigins(link.workspaceExternalId, externalId, {
                widgetOrigins: widgetOrigins
            });
        });
    }
}