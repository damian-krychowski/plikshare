import { Component, input } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Router } from "@angular/router";
import { AuthService } from "../../services/auth.service";

@Component({
    selector: 'app-settings-menu-btn',
    imports: [
        MatButtonModule,
        MatTooltipModule
    ],
    templateUrl: './settings-menu-btn.component.html',
    styleUrl: './settings-menu-btn.component.scss'
})
export class SettingsMenuBtnComponent {
    isDanger = input(false);

    constructor(
        private _router: Router,
        public auth: AuthService
    ){}
      

    public goToAccount() {
        this._router.navigate(['/account']);
    }
} 