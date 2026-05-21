import { Component, HostListener, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { TrashItemDto } from '../../../services/trash.api';
import { FileIconPipe } from '../../../files-explorer/file-icon-pipe/file-icon.pipe';
import { StorageSizePipe } from '../../../shared/storage-size.pipe';

/**
 * A trashed file row. Reuses the shared item-bar styling (so it looks exactly like the
 * explorer's file rows) but carries only what trash needs: the deletion date, the
 * auto-delete date, and a selection checkbox.
 */
@Component({
    selector: 'app-trash-file-item',
    standalone: true,
    imports: [
        DatePipe,
        MatCheckboxModule,
        FileIconPipe,
        StorageSizePipe
    ],
    templateUrl: './trash-file-item.component.html',
    styleUrl: './trash-file-item.component.scss'
})
export class TrashFileItemComponent {
    item = input.required<TrashItemDto>();
    isSelected = input.required<boolean>();

    selectedChange = output<boolean>();
    shiftSelected = output<void>();

    private _shiftOnMouseDown = false;

    // Capture the shift state at the start of the interaction — a shift+click then resolves
    // to a range selection instead of a single toggle. preventDefault stops the browser
    // from text-selecting across rows on shift+click.
    @HostListener('mousedown', ['$event'])
    onMouseDown(event: MouseEvent) {
        this._shiftOnMouseDown = event.shiftKey;

        if (event.shiftKey)
            event.preventDefault();
    }

    toggle() {
        if (this._shiftOnMouseDown) {
            this._shiftOnMouseDown = false;
            this.shiftSelected.emit();
            return;
        }

        this.selectedChange.emit(!this.isSelected());
    }
}
