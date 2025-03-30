import { Pipe, PipeTransform } from '@angular/core';

/**
 * Utility class with static methods for storage size conversions
 */
export class StorageSizeUtils {
    static readonly KB = 1024;
    static readonly MB = StorageSizeUtils.KB * 1024;
    static readonly GB = StorageSizeUtils.MB * 1024;
    static readonly TB = StorageSizeUtils.GB * 1024;

    /**
     * Formats a byte value into a human-readable size with appropriate unit
     */
    public static formatSize(sizeInBytes: number): string {
        const numericValue = StorageSizeUtils.getNumericValue(sizeInBytes);
        const unit = StorageSizeUtils.getUnit(sizeInBytes);
        
        return `${numericValue} ${unit}`;
    }

    /**
     * Gets the numeric value of bytes in the appropriate unit (TB, GB, MB, KB)
     */
    public static getNumericValue(sizeInBytes: number): string {
        if (sizeInBytes >= StorageSizeUtils.TB) {
            return (sizeInBytes / StorageSizeUtils.TB).toFixed(2);
        } else if (sizeInBytes >= StorageSizeUtils.GB) {
            return (sizeInBytes / StorageSizeUtils.GB).toFixed(2);
        } else if (sizeInBytes >= StorageSizeUtils.MB) {
            return (sizeInBytes / StorageSizeUtils.MB).toFixed(2);
        } else {
            return (sizeInBytes / StorageSizeUtils.KB).toFixed(2);
        }
    }

    /**
     * Gets the appropriate unit (TB, GB, MB, KB) for a byte value
     */
    public static getUnit(sizeInBytes: number): string {
        if (sizeInBytes >= StorageSizeUtils.TB) {
            return 'TB';
        } else if (sizeInBytes >= StorageSizeUtils.GB) {
            return 'GB';
        } else if (sizeInBytes >= StorageSizeUtils.MB) {
            return 'MB';
        } else {
            return 'KB';
        }
    }

    /**
     * Converts sizeInBytes to a full number of MB, GB, or TB
     * Returns an object with the value and unit
     * 
     * @param sizeInBytes The size in bytes to convert
     * @returns An object with {value: number, unit: 'MB'|'GB'|'TB'}
     */
    public static convertToFullUnit(sizeInBytes: number): { value: number, unit: 'MB'|'GB'|'TB' } {
        // Determine the appropriate unit based on size
        let unit: 'MB'|'GB'|'TB';
        let value: number;

        if (sizeInBytes >= StorageSizeUtils.TB) {
            unit = 'TB';
            value = Math.floor(sizeInBytes / StorageSizeUtils.TB);
        } else if (sizeInBytes >= StorageSizeUtils.GB) {
            unit = 'GB';
            value = Math.floor(sizeInBytes / StorageSizeUtils.GB);
        } else {
            unit = 'MB';
            value = Math.floor(sizeInBytes / StorageSizeUtils.MB);
        }

        return { value, unit };
    }
    
    /**
     * Converts from a unit-value pair back to bytes
     * 
     * @param unitValue An object with {value: number, unit: 'MB'|'GB'|'TB'}
     * @returns The size in bytes as a number
     */
    public static convertToBytes(unitValue: { value: number, unit: 'MB'|'GB'|'TB' }): number {
        const { value, unit } = unitValue;
        
        switch (unit) {
            case 'TB':
                return value * StorageSizeUtils.TB;
            case 'GB':
                return value * StorageSizeUtils.GB;
            case 'MB':
                return value * StorageSizeUtils.MB;
            default:
                throw new Error(`Unsupported unit: ${unit}`);
        }
    }
}

@Pipe({
    name: 'storageSize',
    standalone: true
})
export class StorageSizePipe implements PipeTransform {
    transform(value: any, ...args: unknown[]): unknown {
        return StorageSizeUtils.formatSize(value);
    }
}

@Pipe({
    name: 'storageSizeValue',
    standalone: true
})
export class StorageSizeValuePipe implements PipeTransform {
    transform(value: any, ...args: unknown[]): unknown {
        return StorageSizeUtils.getNumericValue(value);
    }
}

@Pipe({
    name: 'storageSizeUnit',
    standalone: true
})
export class StorageSizeUnitPipe implements PipeTransform {
    transform(value: any, ...args: unknown[]): unknown {
        return StorageSizeUtils.getUnit(value);
    }
}