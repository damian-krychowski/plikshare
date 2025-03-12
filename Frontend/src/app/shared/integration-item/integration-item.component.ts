import { Component,  input, output, signal, WritableSignal } from "@angular/core";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { EditableTxtComponent } from "../editable-txt/editable-txt.component";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { Operations, OptimisticOperation } from "../../services/optimistic-operation";
import { MatDialog } from "@angular/material/dialog";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";
import { AppIntegrationType, IntegrationsApi } from "../../services/integrations.api";
import { WorkspaceLinkComponenet } from "../workspace-link/workspace-link.component";

export type AppIntegration = {
    externalId: string;
    type: AppIntegrationType;
    workspace: {
        externalId: string;
        name: string;
    };

    name: WritableSignal<string>;
    isActive: WritableSignal<boolean>;

    isNameEditing: WritableSignal<boolean>;
    isHighlighted: WritableSignal<boolean>;
}

@Component({
    selector: 'app-integration-item',
    imports: [
        ConfirmOperationDirective,
        EditableTxtComponent,
        ActionButtonComponent,
        WorkspaceLinkComponenet
    ],
    templateUrl: './integration-item.component.html',
    styleUrl: './integration-item.component.scss'
})
export class IntegrationItemComponent {
    integration = input.required<AppIntegration>();

    edited = output<void>();
    deleted = output<OptimisticOperation>();
    clicked = output<void>();
    activated = output<OptimisticOperation>();  
    deactivated = output<OptimisticOperation>();  
    confirmed = output<void>();

    isHighlighted = observeIsHighlighted(this.integration);

    areActionsVisible = signal(false);

    constructor(
        private _dialog: MatDialog,
        private _integrationsApi: IntegrationsApi
    ) { }


    async delete() {
        const integration = this.integration();
        const operation = Operations.optimistic();

        this.deleted.emit(operation);

        try {
            await this._integrationsApi.deleteIntegration(
                integration.externalId);
            
            operation.succeeded();
        } catch (error) {
            console.error(error);
            operation.failed(error);
        }
    }

    async saveName(newName: string) {
        const integration = this.integration();
        const oldName = integration.name();

        this.integration().name.set(newName);
        
        try {
            await this._integrationsApi.updateName(
                integration.externalId, {
                name: newName
            });            
        } catch (err: any) {
            if(err.error?.code == 'email-provider-name-is-not-unique') {
                this.integration().name.set(oldName);
            } else {
                console.error(err);
            }
        }
    }

    editName() {
        this.integration().isNameEditing.set(true);
        this.areActionsVisible.set(false);
    }

    toggleActions() {
        this.areActionsVisible.update(value => !value);
    }

    onClicked() {
        this.clicked.emit();
    }

    async onActivate() {
        const integration = this.integration();
        integration.isActive.set(true);

        const operation = Operations.optimistic();

        this.activated.emit(operation);

        try {
            await this._integrationsApi.activate(
                integration.externalId);

            operation.succeeded();
        } catch (error) {
            console.error(error);

            integration.isActive.set(false);
            operation.failed(error);
        }
    }

    async onDeactivate() {
        const integration = this.integration();
        integration.isActive.set(false);

        const operation = Operations.optimistic();

        this.deactivated.emit(operation);

        try {
            await this._integrationsApi.deactivate(
                integration.externalId);

            operation.succeeded();
        } catch (error) {
            console.error(error);
            
            integration.isActive.set(false);
            operation.failed(error);
        }
    }
}