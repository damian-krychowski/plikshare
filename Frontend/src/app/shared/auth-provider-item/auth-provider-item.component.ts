import { Component, computed, input, output, signal, Signal, WritableSignal } from "@angular/core";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { AuthProvidersApi } from "../../services/auth-providers.api";
import { Operations, OptimisticOperation } from "../../services/optimistic-operation";
import { MatDialog } from "@angular/material/dialog";
import { EditOidcProviderComponent, EditOidcProviderDialogData } from "../../account/auth-settings/edit-oidc-provider/edit-oidc-provider.component";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";

export type AppAuthProvider = {
    externalId: Signal<string>;
    name: WritableSignal<string>;
    type: Signal<string>;
    isActive: WritableSignal<boolean>;
    clientId: WritableSignal<string>;
    issuerUrl: WritableSignal<string>;

    isNameEditing: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
}

@Component({
    selector: 'app-auth-provider-item',
    imports: [
        ConfirmOperationDirective,
        ActionButtonComponent
    ],
    templateUrl: './auth-provider-item.component.html',
    styleUrl: './auth-provider-item.component.scss'
})
export class AuthProviderItemComponent {
    authProvider = input.required<AppAuthProvider>();

    deleted = output<OptimisticOperation>();
    activated = output<OptimisticOperation>();
    deactivated = output<OptimisticOperation>();

    externalId = computed(() => this.authProvider().externalId());
    name = computed(() => this.authProvider().name());
    type = computed(() => this.authProvider().type());
    isActive = computed(() => this.authProvider().isActive());
    issuerUrl = computed(() => this.authProvider().issuerUrl());

    isHighlighted = observeIsHighlighted(this.authProvider);

    areActionsVisible = signal(false);

    constructor(
        private _dialog: MatDialog,
        private _authProvidersApi: AuthProvidersApi
    ) { }

    async delete() {
        const operation = Operations.optimistic();

        this.deleted.emit(operation);

        try {
            await this._authProvidersApi.deleteAuthProvider(
                this.externalId());

            operation.succeeded();
        } catch (error) {
            console.error(error);
            operation.failed(error);
        }
    }

    onEdit() {
        this.areActionsVisible.set(false);

        const data: EditOidcProviderDialogData = {
            authProvider: this.authProvider()
        };

        this._dialog.open(EditOidcProviderComponent, {
            width: '500px',
            position: {
                top: '100px'
            },
            disableClose: true,
            data: data
        });
    }

    toggleActions() {
        this.areActionsVisible.update(value => !value);
    }

    async onActivate() {
        const authProvider = this.authProvider();
        authProvider.isActive.set(true);

        const operation = Operations.optimistic();

        this.activated.emit(operation);

        try {
            await this._authProvidersApi.activate(
                authProvider.externalId());

            operation.succeeded();
        } catch (error) {
            console.error(error);

            authProvider.isActive.set(false);
            operation.failed(error);
        }
    }

    async onDeactivate() {
        const authProvider = this.authProvider();
        authProvider.isActive.set(false);

        const operation = Operations.optimistic();

        this.deactivated.emit(operation);

        try {
            await this._authProvidersApi.deactivate(
                authProvider.externalId());

            operation.succeeded();
        } catch (error) {
            console.error(error);

            authProvider.isActive.set(true);
            operation.failed(error);
        }
    }
}
