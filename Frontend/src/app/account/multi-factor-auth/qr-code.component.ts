import { Component, ElementRef, input, OnChanges, Renderer2, ViewChild } from "@angular/core"
import { QRCodeErrorCorrectionLevel, QRCodeRenderersOptions, toCanvas } from "qrcode";

@Component({
    selector: "app-qr-code",
    standalone: true,
    template: `<div #qrcElement></div>`,
})
export class QRCodeComponent implements OnChanges {
    qrdata = input.required<string>();
    width = input.required<number>();

    colorDark = input("#000000ff");
    colorLight = input("#ffffffff");
    margin = input(4);
    scale = input(4);
    errorCorrectionLevel = input<QRCodeErrorCorrectionLevel>("M");

    @ViewChild("qrcElement", { static: true }) public qrcElement!: ElementRef

    constructor(
        private renderer: Renderer2
    ) { }

    public async ngOnChanges(): Promise<void> {
        await this.createQRCode()
    }
    
    private async createQRCode(): Promise<void> {
        try {
            const canvasElement: HTMLCanvasElement = this
                .renderer
                .createElement("canvas");

            await this.uriToCanvas(canvasElement, {
                color: {
                    dark: this.colorDark(),
                    light: this.colorLight(),
                },
                errorCorrectionLevel: this.errorCorrectionLevel(),
                margin: this.margin(),
                scale: this.scale(),
                width: this.width(),
            });

            for (const node of this.qrcElement.nativeElement.childNodes) {
                this.renderer.removeChild(this.qrcElement.nativeElement, node)
            }

            this.renderer.appendChild(
                this.qrcElement.nativeElement, 
                canvasElement)
        } catch (e: any) {
            console.error("Error generating QR Code:", e)
        }
    }

    private uriToCanvas(canvas: HTMLCanvasElement, qrCodeConfig: QRCodeRenderersOptions): Promise<void>{
        return new Promise((resolve, reject) => {
            toCanvas(
                canvas,
                this.qrdata(),
                qrCodeConfig,
                (error: Error | null | undefined) => {
                    if (error) {
                        reject(error)
                    } else {
                        resolve();
                    }
                }
            )
        });
    }
}