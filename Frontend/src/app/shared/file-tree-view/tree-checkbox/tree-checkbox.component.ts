import { CommonModule } from "@angular/common";
import { Component, computed, input, output } from "@angular/core";
import { MatCheckboxModule } from "@angular/material/checkbox";

@Component({
    selector: 'app-tree-checkbox',
    imports: [
        CommonModule,
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

    onDivClick() {
        if(this.isParentSelected() && !this.isParentExcluded()) {
            this.isExcludedChange.emit(!this.isExcluded());
        }
    }
}