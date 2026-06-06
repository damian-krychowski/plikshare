import { Directive, HostListener, output, input } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { OperationConfirmComponent } from './operation-confirm.component';

@Directive({
    selector: '[appConfirmOperation]',
    standalone: true,
})
export class ConfirmOperationDirective {
    operationItem = input.required<string>();
    verb = input.required<string>();
    isOperationDanger = input(false);
    isOperationDisabled = input(false);
    operationSubtitle = input<string | undefined>(undefined);
    subtitleLoader = input<(() => Promise<string | undefined>) | undefined>(undefined);
    confirmedClick = output<any>();

    private _isResolvingSubtitle = false;

    constructor(private dialog: MatDialog) { }

    @HostListener('click', ['$event'])
    async onClick(event: Event): Promise<void> {
        event.stopImmediatePropagation();

        if (this.isOperationDisabled())
            return;

        if (this._isResolvingSubtitle)
            return;

        let subtitle = this.operationSubtitle();

        const loader = this.subtitleLoader();

        if (loader) {
            this._isResolvingSubtitle = true;

            try {
                subtitle = await loader();
            } catch {
                subtitle = this.operationSubtitle();
            } finally {
                this._isResolvingSubtitle = false;
            }
        }

        const dialogRef = this.dialog.open(OperationConfirmComponent, {
            width: '400px',
            maxHeight: '600px',
            position: {
                top: '100px'
            },
            data: {
                item: this.operationItem(),
                verb: this.verb(),
                isDanger: this.isOperationDanger(),
                subtitle: subtitle
            }
        });


        dialogRef.afterClosed().subscribe(result => {
            if (result) {
                this.confirmedClick.emit(event);
            }
        });
    }
}
