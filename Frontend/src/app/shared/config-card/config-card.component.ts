import { Component, OnInit, input, signal } from '@angular/core';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';

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
 *
 * <para>The content area also auto-hides when no content is projected (e.g. when a
 * caller conditionally renders its body via <c>@if</c>) so the dashed divider doesn't
 * dangle as a stray underline beneath the header.</para>
 *
 * <para>Opt-in <c>[stretch]="true"</c> makes the card fill the height of its parent —
 * useful when several cards sit in a CSS grid and should align to the tallest. The
 * projected content area also grows to fill, so its bottom edge meets the card edge.</para>
 */
@Component({
    selector: 'app-config-card',
    standalone: true,
    imports: [
        ActionButtonComponent
    ],
    templateUrl: './config-card.component.html',
    styleUrl: './config-card.component.scss',
    host: {
        '[class.app-config-card--stretch]': 'stretch()'
    }
})
export class ConfigCardComponent implements OnInit {
    title = input.required<string>();
    description = input.required<string>();
    mode = input<'stacked' | 'side'>('stacked');
    stretch = input<boolean>(false);
    collapsible = input<boolean>(false);
    initiallyCollapsed = input<boolean>(false);

    isCollapsed = signal(false);

    ngOnInit() {
        this.isCollapsed.set(this.collapsible() && this.initiallyCollapsed());
    }

    toggleCollapsed() {
        this.isCollapsed.set(!this.isCollapsed());
    }
}
