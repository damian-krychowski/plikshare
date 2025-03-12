import { Directive, EventEmitter, HostListener, input, output, Output } from '@angular/core';

@Directive({
  selector: '[debouncedChange]', // Use the directive as an attribute
  standalone: true
})
export class DebouncedChangeDirective {
  debouncedChange = output<string>();
  debounceTime = input(500);

  private debounceTimer?: any;

  @HostListener('input', ['$event.target.value'])
  onInput(value: string): void {
    clearTimeout(this.debounceTimer);
    this.debounceTimer = setTimeout(() => {
      this.debouncedChange.emit(value);
    }, this.debounceTime()); 
  }
}
