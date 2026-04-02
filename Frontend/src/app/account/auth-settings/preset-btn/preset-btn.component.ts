import { Component, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";

@Component({
    selector: 'app-preset-btn',
    imports: [
        MatButtonModule
    ],
    templateUrl: './preset-btn.component.html',
    styleUrl: './preset-btn.component.scss'
})
export class PresetButtonComponent {
    title = input.required<string>();
    subtitle = input.required<string>();

    clicked = output<void>();
}
