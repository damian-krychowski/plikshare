import { Component, computed, input, model, output, signal, WritableSignal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule, TooltipPosition } from "@angular/material/tooltip";

export type CountdownTime = {
    left: number;
    total: number;
}

@Component({
    selector: 'app-action-btn',
    imports: [
        MatButtonModule,
        MatTooltipModule
    ],
    templateUrl: './action-btn.component.html',
    styleUrl: './action-btn.component.scss'
})
export class ActionButtonComponent {
    tooltip = input<string>();
    tooltipPosition = input<TooltipPosition>("above");

    icon = input.required<`icon-${string}`>();
    disabled = input(false);
    danger = input(false);
    countdown = input<CountdownTime | null>(null);
    isLoading = input(false);

    isMouseOver = model<boolean>(false)

    countdownProgress = computed(() => {
        const countdown = this.countdown();

        if(countdown == null)
            return 0;

        return (countdown.left / countdown.total) * 100
    })
    
    clicked = output<void>();
    disabledClicked = output<void>();

    onClick() {
        if(this.disabled() || this.isLoading()){
            this.disabledClicked.emit();
            return;
        }

        this.clicked.emit();
    }
}