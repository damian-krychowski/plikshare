const TEXTRACT_SUPPORTED_EXTENSIONS = ['.pdf', '.png', '.jpeg', '.jpg', '.tiff'];

export class TextractIntegration {
    public static isSupportedForExtension(fileExtension: string): boolean {
        return TEXTRACT_SUPPORTED_EXTENSIONS.includes(fileExtension);
    }
}