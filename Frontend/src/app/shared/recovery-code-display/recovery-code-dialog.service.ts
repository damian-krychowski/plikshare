import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import {
    RecoveryCodeDisplayComponent,
    RecoveryCodeDisplayDialogData
} from './recovery-code-display.component';
import { AppStorageEncryptionType } from '../../services/storages.api';

@Injectable({ providedIn: 'root' })
export class RecoveryCodeDialogService {
    private _dialog = inject(MatDialog);

    async showOnce(
        recoveryCode: string,
        storageName: string,
        encryptionType: AppStorageEncryptionType): Promise<void> {

        const ref = this._dialog.open<
            RecoveryCodeDisplayComponent,
            RecoveryCodeDisplayDialogData,
            boolean
        >(RecoveryCodeDisplayComponent, {
            width: '700px',
            maxWidth: '95vw',
            position: { top: '80px' },
            disableClose: true,
            data: { recoveryCode, storageName, encryptionType }
        });

        await firstValueFrom(ref.afterClosed());
    }
}
