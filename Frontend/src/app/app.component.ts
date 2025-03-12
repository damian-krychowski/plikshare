import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { EntryPageService } from './services/entry-page.service';

@Component({
    selector: 'app-root',
    imports: [
        CommonModule,
        RouterOutlet,
    ],
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
    constructor(
        private _entryPageService: EntryPageService
    ) {  
    }
    
    async ngOnInit(): Promise<void> {
        await this._entryPageService.reload();
    }
}
