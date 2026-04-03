
import { Component } from '@angular/core';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { EntryPageService } from '../../services/entry-page.service';

@Component({
    selector: 'app-terms-page',
    imports: [
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
