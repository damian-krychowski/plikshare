import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { TopBarComponent } from '../shared/top-bar/top-bar.component';
import { FooterComponent } from '../shared/footer/footer.component';

@Component({
    selector: 'app-not-found-page',
    imports: [
        CommonModule,
        TopBarComponent,
        FooterComponent
    ],
    templateUrl: './not-found-page.component.html',
    styleUrl: './not-found-page.component.scss'
})
export class NotFoundPageComponent {
  constructor() {
  }
}
