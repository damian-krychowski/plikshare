import { Router } from "@angular/router";
import { Component, computed, OnInit, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { AuthService } from "../../services/auth.service";
import { MatDialog } from "@angular/material/dialog";
import { insertItem, pushItems, removeItem } from "../../shared/signal-utils";
import { PresetButtonComponent } from "./preset-btn/preset-btn.component";
import { CommonModule } from "@angular/common";
import { AppAuthProvider, AuthProviderItemComponent } from "../../shared/auth-provider-item/auth-provider-item.component";
import { OptimisticOperation } from "../../services/optimistic-operation";
import { CreateOidcProviderComponent } from "./create-oidc-provider/create-oidc-provider.component";
import { AuthProvidersApi, GetAuthProvidersResponse } from "../../services/auth-providers.api";
import { OIDC_PROVIDER_PRESETS, OIDC_PRESET_ORDER } from "./oidc-provider-presets";

@Component({
    selector: 'app-auth-settings',
    imports: [
        CommonModule,
        MatButtonModule,
        MatTooltipModule,
        AuthProviderItemComponent,
        PresetButtonComponent
    ],
    templateUrl: './auth-settings.component.html',
    styleUrl: './auth-settings.component.scss'
})
export class AuthSettingsComponent implements OnInit {
    isLoading = signal(false);
    isInitialized = signal(false);

    authProviders: WritableSignal<AppAuthProvider[]> = signal([]);

    activeAuthProviders = computed(() => this.authProviders().filter(ap => ap.isActive()));
    notActiveAuthProviders = computed(() => this.authProviders().filter(ap => !ap.isActive()));

    isAnyProviderActive = computed(() => this.activeAuthProviders().length > 0);

    presets = OIDC_PROVIDER_PRESETS;
    presetOrder = OIDC_PRESET_ORDER;

    constructor(
        public auth: AuthService,
        private _dialog: MatDialog,
        private _authProvidersApi: AuthProvidersApi,
        private _router: Router
    ) {}

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            await this.loadAuthProviders();
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
            this.isInitialized.set(true);
        }
    }

    private async loadAuthProviders() {
        const result = await this._authProvidersApi.getAuthProviders();

        this.authProviders.set(result.items.map(ap => {
            const authProvider: AppAuthProvider = {
                externalId: signal(ap.externalId),
                name: signal(ap.name),
                type: signal(ap.type),
                isActive: signal(ap.isActive),
                clientId: signal(ap.clientId),
                issuerUrl: signal(ap.issuerUrl),

                isNameEditing: signal(false),
                isHighlighted: signal(false)
            };

            return authProvider;
        }));
    }

    goToAccount() {
        this._router.navigate(['account']);
    }

    onAddOidc(presetKey: string = 'custom') {
        const preset = OIDC_PROVIDER_PRESETS[presetKey];

        const dialogRef = this._dialog.open(CreateOidcProviderComponent, {
            width: '500px',
            position: {
                top: '100px'
            },
            disableClose: true,
            data: { preset }
        });

        dialogRef.afterClosed().subscribe((authProvider: AppAuthProvider) => {
            if(!authProvider) {
                return;
            }

            this.addAuthProvider(authProvider);
        });
    }

    private addAuthProvider(authProvider: AppAuthProvider) {
        pushItems(this.authProviders, authProvider);
    }

    async onAuthProviderDelete(operation: OptimisticOperation, authProvider: AppAuthProvider) {
        const itemRemoved = removeItem(this.authProviders, authProvider);

        const result = await operation.wait();

        if(result.type === 'failure') {
            insertItem(this.authProviders, authProvider, itemRemoved.index);
        }
    }

    async onAuthProviderActivated(operation: OptimisticOperation, authProvider: AppAuthProvider) {
        const result = await operation.wait();

        if(result.type === 'failure') {
            authProvider.isActive.set(false);
        }
    }

    async onAuthProviderDeactivated(operation: OptimisticOperation, authProvider: AppAuthProvider) {
        const result = await operation.wait();

        if(result.type === 'failure') {
            authProvider.isActive.set(true);
        }
    }
}
