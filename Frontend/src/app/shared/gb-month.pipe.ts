import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
    name: 'gbMonth',
    standalone: true
})
export class GbMonthPipe implements PipeTransform {

    transform(value: any, ...args: unknown[]): unknown {
        const KBMonth = 1000;
        const MBMonth = KBMonth * 1000;
        const GBMonth = MBMonth * 1000;
            
        const byteMonths = value / 30;
        const isNegative = byteMonths < 0;
        const absoluteByteMonths = Math.abs(byteMonths);

        let formattedSize;
        let unit;
    
        formattedSize = this.formatNumber(absoluteByteMonths / GBMonth);
    
        return `${isNegative ? '-' : ''}${formattedSize}`;
    }

    private formatNumber(num: number) {
        let str = num.toString();
    
        if (str.indexOf('.') !== -1) {
            let parts = str.split('.');
            let decimalPart = parts[1];
    
            let significantDecimal = decimalPart.length;
            for (let i = 0; i < decimalPart.length; i++) {
                if (decimalPart[i] !== '0' && decimalPart[i] !== '9') {
                    significantDecimal = i + 2;
                    break;
                }
            }
    
            return num
                .toFixed(significantDecimal)
                .replace(/(\.[0-9]*[1-9])0+$|\.0*$/,'$1');
        }
        
        return num;
    }
}
