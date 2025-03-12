import { Injectable } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class TextMeasurementService {
    private canvas: HTMLCanvasElement;
    private context: CanvasRenderingContext2D;

    constructor() {
        this.canvas = document.createElement('canvas');
        this.context = this.canvas.getContext('2d')!;
    }

    measureSegmentWidth(text: string, options: {
        fontSize: string,
        fontFamily: string
    }): number {
        // Set the font context
        this.context.font = `${options.fontSize} ${options.fontFamily}`;
        
        // Measure text width
        const textWidth = this.context.measureText(text).width;
        
        return textWidth;
    }
}