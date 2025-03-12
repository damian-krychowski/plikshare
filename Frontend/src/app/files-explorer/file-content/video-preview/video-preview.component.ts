import { Component, input } from "@angular/core";

@Component({
    selector: 'app-video-preview',
    imports: [],
    templateUrl: './video-preview.component.html',
    styleUrls: ['./video-preview.component.scss']
})
export class VideoPreviewComponent {
    fileUrl = input.required<string>();

    handleMediaError(event: any): void {
        console.error('Error loading media:', event);
    }
}