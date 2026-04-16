import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import {
    RecoveryCodeDisplayComponent,
    RecoveryCodeDisplayDialogData
} from './recovery-code-display.component';

@Injectable({ providedIn: 'root' })
export class RecoveryCodeDialogService {
    private _dialog = inject(MatDialog);

    async show(data: RecoveryCodeDisplayDialogData): Promise<void> {
        const ref = this._dialog.open<
            RecoveryCodeDisplayComponent,
            RecoveryCodeDisplayDialogData,
            boolean
        >(RecoveryCodeDisplayComponent, {
            width: '700px',
            maxWidth: '95vw',
            position: { top: '80px' },
            disableClose: true,
            data
        });

        await firstValueFrom(ref.afterClosed());
    }
}
