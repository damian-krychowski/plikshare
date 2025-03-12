import { CommonModule } from "@angular/common";
import { Component, input } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { Router, RouterLink } from "@angular/router";
import { MenuAnimation } from "../../../shared/menu/menu-animation";
import { MatTooltipModule } from "@angular/material/tooltip";
import { EntryPageService } from "../../../services/entry-page.service";

@Component({
    selector: 'app-footer',
    imports: [
        CommonModule,
        RouterLink,
        MatButtonModule,
        MatTooltipModule
    ],
    templateUrl: './footer.component.html',
    styleUrl: './footer.component.scss',
    animations: [MenuAnimation]
})
export class FooterComponent {
    mode = input<'light' | 'dark'>('dark');
 
    constructor(
        public entryPage: EntryPageService,
        private _router: Router
    ) {
    }    
}