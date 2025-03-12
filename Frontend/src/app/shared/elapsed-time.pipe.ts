import { Pipe, PipeTransform } from "@angular/core";

@Pipe({
    name: 'elapsedTime',
    standalone: true
})
export class ElapsedTimePipe implements PipeTransform {
    transform(seconds: number): string {
        if (seconds < 0) {
            return '0s';
        }

        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const remainingSeconds = seconds % 60;

        // Format based on duration
        if (hours > 0) {
            return `${hours}h ${minutes}m ${remainingSeconds}s`;
        } else if (minutes > 0) {
            return `${minutes}m ${remainingSeconds}s`;
        } else {
            return `${remainingSeconds}s`;
        }
    }
}