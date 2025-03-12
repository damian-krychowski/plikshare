import { Router } from "@angular/router";
import { Component, computed, OnInit, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../services/auth.service";
import { MatDialog } from "@angular/material/dialog";
import { DataStore } from "../../services/data-store.service";
import { insertItem, pushItems, removeItem } from "../../shared/signal-utils";
import { ItemButtonComponent } from "../../shared/buttons/item-btn/item-btn.component";
import { ActionButtonComponent } from "../../shared/buttons/action-btn/action-btn.component";
import { MatFormFieldModule } from "@angular/material/form-field";
import { FormsModule } from "@angular/forms";
import { CommonModule } from "@angular/common";
import { MatInputModule } from "@angular/material/input";
import { CreateAwsSesComponent } from "./aws/create-aws-ses/create-aws-ses.component";
import { AppEmailProvider, EmailProviderItemComponent } from "../../shared/email-provider-item/email-provider-item.component";
import { OptimisticOperation } from "../../services/optimistic-operation";
import { CreateResendComponent } from "./resend/create-resend/create-resend.component";
import { ActionTextButtonComponent } from "../../shared/buttons/action-text-btn/action-text-btn.component";
import { CreateSmtpComponent } from "./smtp/create-smtp/create-smtp.component";

@Component({
    selector: 'app-email-settings',
    imports: [
        CommonModule,
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatTooltipModule,
        EmailProviderItemComponent,
        ItemButtonComponent,
        ActionButtonComponent,
        ActionTextButtonComponent
    ],
    templateUrl: './email-settings.component.html',
    styleUrl: './email-settings.component.scss'
})
export class EmailSettingsComponent implements OnInit {       
    isLoading = signal(false);
    isInitialized = signal(false);

    emailProviders: WritableSignal<AppEmailProvider[]> = signal([]);    

    activeEmailProviders = computed(() => this.emailProviders().filter(ep => ep.isActive()));
    notActiveEmailProviders = computed(() => this.emailProviders().filter(ep => !ep.isActive()));

    isAnyProviderActive = computed(() => this.activeEmailProviders().length > 0);

    constructor(
        public auth: AuthService,
        private _dialog: MatDialog,
        private _dataStore: DataStore,
        private _router: Router
    ) {}

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            const loadings = [
                this.loadEmailProviders()
            ];

            await Promise.all(loadings);
        } catch (error) {
            console.error(error);    
        } finally {
            this.isLoading.set(false);
            this.isInitialized.set(true);
        }
    }

    private async loadEmailProviders() {
        const result = await this._dataStore.getEmailProviders();

        this.emailProviders.set(result.items.map(ep => {
            const emailProvider: AppEmailProvider = {
                externalId: signal(ep.externalId),
                name: signal(ep.name),
                type: signal(ep.type),
                emailFrom: signal(ep.emailFrom),
                isActive: signal(ep.isActive),
                isConfirmed: signal(ep.isConfirmed),

                isNameEditing: signal(false),
                isHighlighted: signal(false)
            };

            return emailProvider;
        }));
    }

    goToAccount() {
        this._router.navigate(['account']);
    }

    onAddSmtp() {
        const dialogRef = this._dialog.open(CreateSmtpComponent, {
            width: '500px',
            position: {
                top: '100px'
            },
            disableClose: true
        });

        dialogRef.afterClosed().subscribe((emailProvider: AppEmailProvider) => {
            if(!emailProvider)
                return;

            this.addEmailProvider(emailProvider);
        });
    }

    onAddResend() { 
        const dialogRef = this._dialog.open(CreateResendComponent, {
            width: '500px',
            position: {
                top: '100px'
            },
            disableClose: true
        });

        dialogRef.afterClosed().subscribe((emailProvider: AppEmailProvider) => {
            if(!emailProvider)
                return;

            this.addEmailProvider(emailProvider);
        });
    }

    onAddAwsSes() {
        const dialogRef = this._dialog.open(CreateAwsSesComponent, {
            width: '500px',
            position: {
                top: '100px'
            },
            disableClose: true
        });

        dialogRef.afterClosed().subscribe((emailProvider: AppEmailProvider) => {
            if(!emailProvider)
                return;

            this.addEmailProvider(emailProvider);
        });
    }

    private addEmailProvider(emailProvider: AppEmailProvider) {
        pushItems(this.emailProviders, emailProvider);
    }

    async onEmailProviderDelete(operation: OptimisticOperation, emailProvider: AppEmailProvider) {
        const itemRemoved = removeItem(this.emailProviders, emailProvider);

        const result = await operation.wait();

        if(result.type === 'failure') {
            insertItem(this.emailProviders, emailProvider, itemRemoved.index);
        }
    }

    async onEmailProviderActivated(operation: OptimisticOperation, emailProvider: AppEmailProvider) {
        const activeEmailProviders = this
            .emailProviders()
            .filter(ep => ep.isActive() && ep != emailProvider);
            
        for (const activeEmailProvider of activeEmailProviders) {
            activeEmailProvider.isActive.set(false);
        }

        const result = await operation.wait();

        if(result.type === 'failure') {
            for (const activeEmailProvider of activeEmailProviders) {
                activeEmailProvider.isActive.set(true);
            }
        }
    }
}