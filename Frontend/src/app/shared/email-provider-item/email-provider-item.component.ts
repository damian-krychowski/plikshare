import { Component, computed, input, output, signal, Signal, WritableSignal } from "@angular/core";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { AppEmailProviderType, EmailProvidersApi } from "../../services/email-providers.api";
import { Operations, OptimisticOperation } from "../../services/optimistic-operation";
import { MatDialog } from "@angular/material/dialog";
import { ConfirmEmailProviderComponent } from "../../account/email-settings/confirm-email-provider/confirm-email-provider.component";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";

export type AppEmailProvider = {
    externalId: Signal<string>;
    name: WritableSignal<string>;
    type: Signal<AppEmailProviderType>;
    emailFrom: Signal<string>;
    isConfirmed: WritableSignal<boolean>;
    isActive: WritableSignal<boolean>;

    isNameEditing: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
}

@Component({
    selector: 'app-email-provider-item',
    imports: [
        ConfirmOperationDirective,
        EditableTxtComponent,
        ActionButtonComponent
    ],
    templateUrl: './email-provider-item.component.html',
    styleUrl: './email-provider-item.component.scss'
})
export class EmailProviderItemComponent {
    emailProvider = input.required<AppEmailProvider>();

    edited = output<void>();
    deleted = output<OptimisticOperation>();
    clicked = output<void>();
    activated = output<OptimisticOperation>();  
    deactivated = output<OptimisticOperation>();  
    confirmed = output<void>();

    externalId = computed(() => this.emailProvider().externalId());
    name = computed(() => this.emailProvider().name());
    type = computed(() => this.emailProvider().type());
    emailFrom = computed(() => this.emailProvider().emailFrom())
    isActive = computed(() => this.emailProvider().isActive())
    isConfirmed = computed(() => this.emailProvider().isConfirmed());
    
    isHighlighted = observeIsHighlighted(this.emailProvider);
    isNameEditing = computed(() => this.emailProvider().isNameEditing());

    areActionsVisible = signal(false);

    constructor(
        private _dialog: MatDialog,
        private _emailProvidersApi: EmailProvidersApi
    ) { }


    async delete() {
        const operation = Operations.optimistic();

        this.deleted.emit(operation);

        try {
            await this._emailProvidersApi.deleteEmailProvider(
                this.externalId());
            
            operation.succeeded();
        } catch (error) {
            console.error(error);
            operation.failed(error);
        }
    }

    async saveName(newName: string) {
        const oldName = this.name();
        this.emailProvider().name.set(newName);
        
        try {
            await this._emailProvidersApi.updateName(
                this.externalId(), {
                name: newName
            });            
        } catch (err: any) {
            if(err.error?.code == 'email-provider-name-is-not-unique') {
                this.emailProvider().name.set(oldName);
            } else {
                console.error(err);
            }
        }
    }

    editName() {
        this.emailProvider().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    toggleActions() {
        this.areActionsVisible.update(value => !value);
    }

    onClicked() {
        this.clicked.emit();
    }

    onConfirm() {  
        const confirmationDialogRef = this._dialog.open(ConfirmEmailProviderComponent, {
            width: '500px',
            position: {
                top: '100px'
            },
            data: {
                emailProvider: this.emailProvider()
            }
        });

        confirmationDialogRef.afterClosed().subscribe((confirmationResult: boolean | undefined) => {
            if(!confirmationResult)
                return;

            this.confirmed.emit();
        });
    }   

    async onActivate() {
        const emailProvider = this.emailProvider();
        emailProvider.isActive.set(true);

        const operation = Operations.optimistic();

        this.activated.emit(operation);

        try {
            await this._emailProvidersApi.activate(
                emailProvider.externalId());

            operation.succeeded();
        } catch (error) {
            console.error(error);

            emailProvider.isActive.set(false);
            operation.failed(error);
        }
    }

    async onDeactivate() {
        const emailProvider = this.emailProvider();
        emailProvider.isActive.set(false);

        const operation = Operations.optimistic();

        this.deactivated.emit(operation);

        try {
            await this._emailProvidersApi.deactivate(
                emailProvider.externalId());

            operation.succeeded();
        } catch (error) {
            console.error(error);
            
            emailProvider.isActive.set(false);
            operation.failed(error);
        }
    }
}