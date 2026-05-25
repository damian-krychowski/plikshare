import { Component, input } from '@angular/core';

/**
 * Self-contained config section card: a title + description block plus projected
 * configuration controls (selects/inputs/buttons).
 *
 * Two layout modes:
 *  - 'stacked' (default): controls sit below the title/description, left-aligned.
 *  - 'side': controls sit to the right of the title/description, vertically centered.
 *
 * In stacked mode, an optional right-side header slot is available via the
 * `[configCardActions]` attribute selector — typically used for toggles or
 * pending-change action buttons that should sit next to the title. The slot
 * collapses when nothing is projected and is ignored in 'side' mode.
 */
@Component({
    selector: 'app-config-card',
    standalone: true,
    imports: [],
    templateUrl: './config-card.component.html',
    styleUrl: './config-card.component.scss'
})
export class ConfigCardComponent {
    title = input.required<string>();
    description = input.required<string>();
    mode = input<'stacked' | 'side'>('stacked');
}
