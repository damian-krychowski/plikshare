import { Directive, Output, EventEmitter, HostListener, output } from '@angular/core';

@Directive({
  selector: '[prefetch]',
  standalone: true
})
export class PrefetchDirective {

  prefetch = output<void>();

  private timeoutId: any;

  constructor() { }

  @HostListener('mouseenter') onMouseEnter() {
    this.timeoutId = setTimeout(() => this.prefetch.emit(), 50);
  }

  @HostListener('mouseleave') onMouseLeave() {
    clearTimeout(this.timeoutId);
  }
}
