import { CommonModule } from "@angular/common";
import { Component, OnInit, signal } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { Router, RouterLink } from "@angular/router";
import { MenuAnimation } from "../../../shared/menu/menu-animation";
import { MatTooltipModule } from "@angular/material/tooltip";
import { EntryPageService } from "../../../services/entry-page.service";
import { toggle } from "../../../shared/signal-utils";

@Component({
    selector: 'app-top-bar',
    imports: [
        CommonModule,
        RouterLink,
        MatButtonModule,
        MatTooltipModule
    ],
    templateUrl: './top-bar.component.html',
    styleUrl: './top-bar.component.scss',
    animations: [MenuAnimation]
})
export class TopBarComponent {
    isMenuOpen = signal(false);

    constructor(
        public entryPage: EntryPageService,
        private _router: Router
    ) {
    }

    toggleMenu() {
        toggle(this.isMenuOpen);
    }

    isUrlActive(path: string) {
        const url = this._router.createUrlTree([path]);

        return this._router.isActive(url, {
            paths: 'subset',
            queryParams: 'ignored',
            fragment: 'ignored',
            matrixParams: 'ignored'
        });
    }
}