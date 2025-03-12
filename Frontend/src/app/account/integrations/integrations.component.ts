import { Router } from "@angular/router";
import { Component, computed, OnInit, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../services/auth.service";
import { DataStore } from "../../services/data-store.service";
import { insertItem, pushItems, removeItem } from "../../shared/signal-utils";
import { ItemButtonComponent } from "../../shared/buttons/item-btn/item-btn.component";
import { MatFormFieldModule } from "@angular/material/form-field";
import { FormsModule } from "@angular/forms";
import { CommonModule } from "@angular/common";
import { MatInputModule } from "@angular/material/input";
import { OptimisticOperation } from "../../services/optimistic-operation";
import { AppIntegration, IntegrationItemComponent } from "../../shared/integration-item/integration-item.component";

@Component({
    selector: 'app-integrations',
    imports: [
        CommonModule,
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatTooltipModule,
        IntegrationItemComponent,
        ItemButtonComponent,
    ],
    templateUrl: './integrations.component.html',
    styleUrl: './integrations.component.scss'
})
export class IntegrationsComponent implements OnInit {       
    isLoading = signal(false);
    isInitialized = signal(false);

    integrations: WritableSignal<AppIntegration[]> = signal([]);    

    activeIntegrations = computed(() => this.integrations().filter(i => i.isActive()));
    notActiveIntegrations = computed(() => this.integrations().filter(i => !i.isActive()));

    constructor(
        public auth: AuthService,
        private _dataStore: DataStore,
        private _router: Router
    ) {}

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            const loadings = [
                this.loadIntegrations()
            ];

            await Promise.all(loadings);
        } catch (error) {
            console.error(error);    
        } finally {
            this.isLoading.set(false);
            this.isInitialized.set(true);
        }
    }

    private async loadIntegrations() {
        const result = await this._dataStore.getIntegrations();

        this.integrations.set(result.items.map(i => {
            const integration: AppIntegration = {
                externalId: i.externalId,
                name: signal(i.name),
                type: i.type,
                isActive: signal(i.isActive),
                workspace: i.workspace,

                isNameEditing: signal(false),
                isHighlighted: signal(false)
            };

            return integration;
        }));
    }

    goToAccount() {
        this._router.navigate(['account']);
    }

    onAddAwsTextract() {
        this._router.navigate(['settings/integrations/add/aws-textract']);     
    }

    onAddOpenAIChatGPT() {
        this._router.navigate(['settings/integrations/add/openai-chatgpt']);     
    }

    async onIntegrationDelete(operation: OptimisticOperation, integration: AppIntegration) {
        const itemRemoved = removeItem(this.integrations, integration);

        const result = await operation.wait();

        if(result.type === 'failure') {
            insertItem(this.integrations, integration, itemRemoved.index);
        }

        this._dataStore.clear();
    }

    async onIntegrationActivated(operation: OptimisticOperation, integration: AppIntegration) {
        const result = await operation.wait();
        this._dataStore.clear();
    }
}