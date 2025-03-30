import { CommonModule } from "@angular/common";
import { Component, computed, input } from "@angular/core";
import { StorageSizePipe } from "../storage-size.pipe";

@Component({
    selector: 'app-workspace-size',
    imports: [
        CommonModule,
        StorageSizePipe
    ],
    template: `
        @let maxSizeInBytesVal = maxSizeInBytes();

        @if(maxSizeInBytesVal != null) {
          <span [class.color-danger]="isMaxSizeExceeded()">
            {{ currentSizeInBytes() | storageSize }} / {{ maxSizeInBytesVal | storageSize }}
          </span>
        } @else {  
            {{ currentSizeInBytes() | storageSize }}
        }       
    `,
    styles: []
})
export class WorkspaceSizeComponent {
    currentSizeInBytes = input.required<number>();
    maxSizeInBytes = input.required<number | null>();

    isMaxSizeExceeded = computed(() => {
        const maxSizeInBytes = this.maxSizeInBytes();

        if (maxSizeInBytes == null)
            return false;

        return maxSizeInBytes < this.currentSizeInBytes();
    });
}