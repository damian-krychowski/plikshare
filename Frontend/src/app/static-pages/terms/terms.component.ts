import { CommonModule } from '@angular/common';
import { Component, computed } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { DomSanitizer, Meta } from '@angular/platform-browser';
import { EntryPageService } from '../../services/entry-page.service';

@Component({
    selector: 'app-terms-page',
    imports: [
        CommonModule,
        TopBarComponent,
        FooterComponent
    ],
    templateUrl: './terms.component.html',
    styleUrl: './terms.component.scss'
})
export class TermsPageComponent {
    constructor(
        public entryPage: EntryPageService
    ) {
    }
}
