import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';
import { EntryPageService } from '../../services/entry-page.service';

@Component({
    selector: 'app-privacy-policy-page',
    imports: [
        CommonModule,
        TopBarComponent,
        FooterComponent
    ],
    templateUrl: './privacy-policy.component.html',
    styleUrl: './privacy-policy.component.scss'
})
export class PrivacyPolicyPageComponent {

    
    constructor(
        public entryPage: EntryPageService
    ){
    }
}
