import { Signal } from '@angular/core';

export type AppUser = {
    email: Signal<string>;
    externalId: string;
}