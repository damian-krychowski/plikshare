
import { Component, computed, DestroyRef, ElementRef, inject, input, output } from "@angular/core";
import { MatCheckboxModule } from "@angular/material/checkbox";

@Component({
    selector: 'app-tree-checkbox',
    imports: [
    MatCheckboxModule
],
    templateUrl: './tree-checkbox.component.html',
    styleUrls: ['./tree-checkbox.component.scss']
})
export class TreeCheckobxComponent {
    isSelected = input.required<boolean>();
    isParentSelected = input.required<boolean>();
    isExcluded = input.required<boolean>();
    isParentExcluded = input.required<boolean>();

    isSelectedChange = output<boolean>();
    isExcludedChange = output<boolean>();
    
    isCheckboxSelected = computed(() => this.isSelected() || this.isParentSelected());
    isCheckboxExcluded = computed(() => this.isExcluded() || this.isParentExcluded())

    constructor() {
        // Make a shift-click on the checkbox behave like a shift-click on the
        // whole row (range selection). Has to run in the CAPTURE phase on the
        // host: mat-checkbox swallows the native input's click (stops its
        // propagation), so a bubble-phase handler on a wrapper never sees it.
        // In capture we get the event before it reaches the input — cancel the
        // native toggle (preventDefault + stopPropagation so no toggle / no
        // change fires) and re-dispatch the click on the enclosing .virtual-row,
        // whose handler owns the range-select logic.
        const host = inject(ElementRef<HTMLElement>).nativeElement;

        const onClickCapture = (event: MouseEvent) => {
            if (!event.shiftKey)
                return;

            event.preventDefault();
            event.stopPropagation();

            host.closest('.virtual-row')?.dispatchEvent(new MouseEvent('click', {
                bubbles: true,
                cancelable: true,
                shiftKey: true
            }));
        };

        host.addEventListener('click', onClickCapture, { capture: true });

        inject(DestroyRef).onDestroy(() =>
            host.removeEventListener('click', onClickCapture, { capture: true }));
    }

    onDivClick() {
        if(this.isParentSelected() && !this.isParentExcluded()) {
            this.isExcludedChange.emit(!this.isExcluded());
        }
    }
}