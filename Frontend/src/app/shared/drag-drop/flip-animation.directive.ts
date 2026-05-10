import { Directive, ElementRef, inject } from '@angular/core';
import { DragStateService, getDraggedExternalId } from '../../services/drag-state.service';

@Directive({
    selector: '[appFlipAnimation]',
    standalone: true,
    exportAs: 'appFlipAnimation'
})
export class FlipAnimationDirective {
    private static readonly FLIP_KEY_ATTR = 'data-flip-key';
    private static readonly DURATION_MS = 200;
    private static readonly EASING = 'cubic-bezier(0.2, 0, 0, 1)';

    private host = inject(ElementRef<HTMLElement>);
    private dragState = inject(DragStateService);

    private rects = new Map<string, DOMRect>();
    private pending = false;

    capture(): void {
        const container = this.host.nativeElement;
        this.rects.clear();
        for (const child of Array.from(container.children) as HTMLElement[]) {
            const key = child.getAttribute(FlipAnimationDirective.FLIP_KEY_ATTR);
            if (key) this.rects.set(key, child.getBoundingClientRect());
        }
    }

    schedule(): void {
        if (this.pending) return;
        this.pending = true;
        setTimeout(() => {
            this.pending = false;
            this.play();
        }, 0);
    }

    private play(): void {
        const container = this.host.nativeElement;
        const dragged = this.dragState.draggedItem();
        const draggedKey = dragged ? getDraggedExternalId(dragged) : null;

        for (const child of Array.from(container.children) as HTMLElement[]) {
            const key = child.getAttribute(FlipAnimationDirective.FLIP_KEY_ATTR);
            if (!key) continue;
            if (key === draggedKey) continue;

            const oldRect = this.rects.get(key);
            if (!oldRect) continue;

            const newRect = child.getBoundingClientRect();
            const dy = oldRect.top - newRect.top;
            if (Math.abs(dy) < 1) continue;

            child.style.transition = 'none';
            child.style.transform = `translateY(${dy}px)`;
            void child.offsetHeight;

            requestAnimationFrame(() => {
                child.style.transition = `transform ${FlipAnimationDirective.DURATION_MS}ms ${FlipAnimationDirective.EASING}`;
                child.style.transform = '';
                const onEnd = () => {
                    child.style.transition = '';
                    child.style.transform = '';
                    child.removeEventListener('transitionend', onEnd);
                };
                child.addEventListener('transitionend', onEnd);
            });
        }
        this.rects.clear();
    }
}
