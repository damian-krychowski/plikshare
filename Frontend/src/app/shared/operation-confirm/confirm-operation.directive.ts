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
    confirmedClick = output<any>();


    constructor(private dialog: MatDialog) { }

    @HostListener('click', ['$event'])
    onClick(event: Event): void {
        event.stopImmediatePropagation();

        if (this.isOperationDisabled())
            return;

        const dialogRef = this.dialog.open(OperationConfirmComponent, {
            width: '400px',
            maxHeight: '600px',
            position: {
                top: '100px'
            },
            data: {
                item: this.operationItem(),
                verb: this.verb(),
                isDanger: this.isOperationDanger()
            }
        });


        dialogRef.afterClosed().subscribe(result => {
            if (result) {
                this.confirmedClick.emit(event);
            }
        });
    }
}
