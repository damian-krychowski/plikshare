import { Component, computed, HostBinding, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule, TooltipPosition } from "@angular/material/tooltip";

@Component({
    selector: 'app-permission-btn',
    imports: [
        MatButtonModule,
        MatTooltipModule
    ],
    templateUrl: './permission-btn.component.html',
    styleUrl: './permission-btn.component.scss'
})
export class PermissionButtonComponent {
    isSelected = input.required<boolean>();
    isReadOnly = input(false);
    
    tooltip = input.required<string>();
    tooltipPosition = input<TooltipPosition>("below");
    
    icon = input.required<`icon-${string}`>();

    isVisible = computed(() => {
        if(this.isReadOnly())
            return this.isSelected();

        return true;
    });
    
    clicked = output<void>();

    onClicked(event: Event) {
        event.stopPropagation();

        if(this.isReadOnly())
            return;

        this.clicked.emit();
    }

    @HostBinding('style.display') get displayStyle() {
        return this.isVisible() ? null : 'none';
    }
}