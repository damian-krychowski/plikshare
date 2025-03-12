// relative-time.component.ts
import { Component, computed, input, signal } from "@angular/core";
import { DatePipe } from "@angular/common";
import { getRelativeTime } from "../../services/time.service";

@Component({
    selector: 'app-relative-time',
    standalone: true,
    imports: [DatePipe],
    template: `
        <span 
            class="relative-time" 
            (mouseenter)="showFullDate.set(true)" 
            (mouseleave)="showFullDate.set(false)">
            {{ showFullDate() ? (datetime() | date:'yyyy-MM-dd HH:mm') : relativeTime() }}
        </span>
    `,
    styleUrl: './relative-time.component.scss',
})
export class RelativeTimeComponent {
    datetime = input.required<string>();
    showFullDate = signal<boolean>(false);
    relativeTime = computed(() => getRelativeTime(this.datetime()));
}