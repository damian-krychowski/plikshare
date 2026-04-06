import { Injectable } from "@angular/core";
import { NativeDateAdapter } from "@angular/material/core";

@Injectable()
export class IsoDateAdapter extends NativeDateAdapter {

    override format(date: Date, _displayFormat: Object): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    override parse(value: string): Date | null {
        if (!value) return null;

        const match = value.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/);
        if (match) {
            const year = parseInt(match[1], 10);
            const month = parseInt(match[2], 10) - 1;
            const day = parseInt(match[3], 10);
            const date = new Date(year, month, day);

            if (date.getFullYear() === year && date.getMonth() === month && date.getDate() === day) {
                return date;
            }
        }

        return super.parse(value);
    }
}
