import { Component, OnInit } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatBadgeModule } from "@angular/material/badge";
import { Router } from "@angular/router";
import { FullEncryptionSessionsStore } from "../../services/full-encryption-sessions.store";

@Component({
    selector: 'app-full-encryption-sessions-btn',
    imports: [
        MatButtonModule,
        MatTooltipModule,
        MatBadgeModule
    ],
    templateUrl: './full-encryption-sessions-btn.component.html',
    styleUrl: './full-encryption-sessions-btn.component.scss'
})
export class FullEncryptionSessionsBtnComponent implements OnInit {
    constructor(
        private _router: Router,
        public store: FullEncryptionSessionsStore
    ) {}

    async ngOnInit(): Promise<void> {
        try {
            await this.store.ensureLoaded();
        } catch (error) {
            console.error(error);
        }
    }

    public goToSessions() {
        this._router.navigate(['/full-encryption-sessions']);
    }
}
