import { Component, ElementRef, ViewChild, computed, effect, input, output, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { SelectAllTextDirective } from "../select-all-text.directive";

@Component({
    selector: 'app-editable-txt',
    imports: [
        FormsModule,
        SelectAllTextDirective
    ],
    templateUrl: './editable-txt.component.html',
    styleUrl: './editable-txt.component.scss'
})
export class EditableTxtComponent {   
    isEditing = input.required<boolean>();
    text = input.required<string>();
    textToDisplay = input<string>();
    canEdit = input(true);

    visibleText = computed(() => this.textToDisplay() ?? this.text());

    valueChange = output<string>();
    editingStopped = output<void>();
    editingStarted = output<void>();

    public newValue: string = '';

    @ViewChild('mirrorSpan') mirrorSpan!: ElementRef<HTMLSpanElement>;
    inputWidth = signal(50);

    constructor() {
        effect(() => {
            if(this.isEditing()) {
                this.newValue = this.text();
                this.adjustInputWidth();
            }
        });
    }

    public adjustInputWidth() {
        setTimeout(() => {
            const spanWidth = this.mirrorSpan?.nativeElement.offsetWidth ?? 0;
            this.inputWidth.set(spanWidth > 50 ? spanWidth + 10 : 50); // Use 50 as the min-width or adjust as needed
        });
    }
    
    public startEditing() {
        this.editingStarted.emit();
    }

    public save() {
        if(!this.isEditing()) {
            return;
        }

        const isValueChanged = this.text() !== this.newValue;

        if (isValueChanged) {
            this.valueChange.emit(this.newValue);
        }

        this.editingStopped.emit()
    } 
}