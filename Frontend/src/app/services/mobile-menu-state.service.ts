import { Injectable, signal } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class MobileMenuStateService {
    isOpen = signal(false);
}
