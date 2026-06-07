import { Component, computed, input } from '@angular/core';

@Component({
    selector: 'app-batch-progress',
    standalone: true,
    imports: [],
    templateUrl: './batch-progress.component.html',
    styleUrl: './batch-progress.component.scss'
})
export class BatchProgressComponent {
    label = input<string>('');
    done = input.required<number>();
    total = input.required<number>();
    failed = input<number>(0);

    percent = computed(() => {
        const total = this.total();
        return total > 0 ? Math.round((this.done() / total) * 100) : 0;
    });
}
