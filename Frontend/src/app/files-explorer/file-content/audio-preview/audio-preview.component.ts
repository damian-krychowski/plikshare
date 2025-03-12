import { Component, input } from "@angular/core";

@Component({
    selector: 'app-audio-preview',
    imports: [],
    templateUrl: './audio-preview.component.html',
    styleUrls: ['./audio-preview.component.scss']
})
export class AudioPreviewComponent {
    fileUrl = input.required<string>();

    handleMediaError(event: any): void {
        console.error('Error loading media:', event);
    }
}