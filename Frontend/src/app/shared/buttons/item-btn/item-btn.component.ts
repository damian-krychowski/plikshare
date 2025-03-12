import { Component, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";

@Component({
    selector: 'app-item-btn',
    imports: [
        MatButtonModule
    ],
    templateUrl: './item-btn.component.html',
    styleUrl: './item-btn.component.scss'
})
export class ItemButtonComponent {
    title = input.required<string>();
    subtitle = input.required<string>();
    icon = input.required<`icon-${string}`>();

    clicked = output<void>();
}