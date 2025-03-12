import { Component, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule, TooltipPosition } from "@angular/material/tooltip";

@Component({
    selector: 'app-action-text-btn',
    imports: [
        MatButtonModule,
        MatTooltipModule
    ],
    templateUrl: './action-text-btn.component.html',
    styleUrl: './action-text-btn.component.scss'
})
export class ActionTextButtonComponent {
    tooltip = input.required<string>();
    tooltipPosition = input<TooltipPosition>("above");

    text = input.required<string>();
    disabled = input(false);
    
    clicked = output<void>();

    onClick() {
        if(this.disabled())
            return;

        this.clicked.emit();
    }
}