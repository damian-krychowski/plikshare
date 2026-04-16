import { Component } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatDialog } from "@angular/material/dialog";
import { firstValueFrom } from "rxjs";
import { AuthService } from "../../services/auth.service";
import { UserEncryptionPasswordApi } from "../../services/user-encryption-password.api";
import { UnlockFullEncryptionComponent } from "../unlock-full-encryption/unlock-full-encryption.component";
import { ConfirmLockEncryptionComponent } from "./confirm-lock-encryption.component";

@Component({
    selector: 'app-full-encryption-sessions-btn',
    imports: [
        MatButtonModule,
        MatTooltipModule
    ],
    templateUrl: './full-encryption-sessions-btn.component.html',
    styleUrl: './full-encryption-sessions-btn.component.scss'
})
export class FullEncryptionSessionsBtnComponent {
    constructor(
        public auth: AuthService,
        private _encryptionApi: UserEncryptionPasswordApi,
        private _dialog: MatDialog
    ) {}

    openUnlockDialog() {
        this._dialog.open(UnlockFullEncryptionComponent, {
            width: '500px',
            position: { top: '100px' },
            disableClose: true
        });
    }

    async openLockDialog() {
        const ref = this._dialog.open<ConfirmLockEncryptionComponent, void, boolean>(
            ConfirmLockEncryptionComponent, {
                width: '400px',
                position: { top: '100px' }
            });

        const confirmed = await firstValueFrom(ref.afterClosed());

        if (confirmed === true) {
            try {
                await this._encryptionApi.lock();
                this.auth.notifyEncryptionLocked();
            } catch (err) {
                console.error('Failed to lock encryption session', err);
            }
        }
    }
}
