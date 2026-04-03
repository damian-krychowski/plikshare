import { Component, OnInit } from '@angular/core';

import { RouterOutlet } from '@angular/router';
import { EntryPageService } from './services/entry-page.service';
import { AntiforgeryApi } from './services/antiforgery.api';

@Component({
    selector: 'app-root',
    imports: [
    RouterOutlet
],
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
    constructor(
        private _antiforgeryApi: AntiforgeryApi,
        private _entryPageService: EntryPageService
    ) {  
    }
    
    async ngOnInit(): Promise<void> {
        await Promise.all([
            this._antiforgeryApi.fetchForAnonymousOrInternal(),
            this._entryPageService.reload()]);
    }
}
