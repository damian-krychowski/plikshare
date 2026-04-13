import { Component, OnInit, signal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { FullEncryptionSessionsStore } from "../services/full-encryption-sessions.store";
import { ActionButtonComponent } from "../shared/buttons/action-btn/action-btn.component";
import { ConfirmOperationDirective } from "../shared/operation-confirm/confirm-operation.directive";

@Component({
    selector: 'app-full-encryption-sessions',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        ActionButtonComponent,
        ConfirmOperationDirective
    ],
    templateUrl: './full-encryption-sessions.component.html',
    styleUrl: './full-encryption-sessions.component.scss'
})
export class FullEncryptionSessionsComponent implements OnInit {
    isLoading = signal(false);

    constructor(
        public store: FullEncryptionSessionsStore,
        private _router: Router
    ) {}

    async ngOnInit(): Promise<void> {
        this.isLoading.set(true);

        try {
            await this.store.load();
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoading.set(false);
        }
    }

    goBack() {
        this._router.navigate(['workspaces']);
    }

    async lock(storageExternalId: string) {
        try {
            await this.store.lock(storageExternalId);
        } catch (error) {
            console.error(error);
        }
    }

    async lockAll() {
        try {
            await this.store.lockAll();
        } catch (error) {
            console.error(error);
        }
    }
}
