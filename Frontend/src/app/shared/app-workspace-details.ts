import { Signal } from "@angular/core";

export type AppWorkspaceDetails = {
    externalId: string;
    name: Signal<string>;
    storageName: Signal<string>;
}