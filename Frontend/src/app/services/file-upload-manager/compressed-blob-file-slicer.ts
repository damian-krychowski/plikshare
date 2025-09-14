import { IFileSlicer } from "./file-upload-manager";

export class CompressedBlobSlicer implements IFileSlicer {
    private _ds: DecompressionStream;
    private _decompressedStream: ReadableStream<Uint8Array>;
    private _reader: ReadableStreamDefaultReader<Uint8Array>;

    constructor(
        private _uncompressedFileSize: number,
        private _compressedBlob: Blob
    ){
        if (!this.isDecompressionSupported()) {
            throw new Error('Decompression is not supported in this browser');
        }

        this._ds = new DecompressionStream('deflate-raw');
        this._decompressedStream = this._compressedBlob.stream().pipeThrough(this._ds);
        this._reader = this._decompressedStream.getReader();
    }

    isDecompressionSupported() {
        return (typeof DecompressionStream === 'function');
    }

    canBeProcessedInParallel(){
        return false;
    }

    // Track where we are in the decompressed stream
    private _currentPosition = 0;
    // Buffer for data that didn't fit in the last slice
    private _remainingBuffer = new Uint8Array(0);

    async takeSlice(start: number, end: number): Promise<Blob> {
        // Verify sequential order
        if (start !== this._currentPosition) {
            throw new Error(`Invalid slice request. Expected start at ${this._currentPosition}, got ${start}`);
        }

        const sliceSize = end - start;
        let resultBuffer = new Uint8Array(sliceSize);
        let bytesCollected = 0;

        // First, use any remaining data from previous slice
        if (this._remainingBuffer.length > 0) {
            const bytesToUse = Math.min(this._remainingBuffer.length, sliceSize);
            resultBuffer.set(this._remainingBuffer.subarray(0, bytesToUse));
            bytesCollected = bytesToUse;
            
            this._remainingBuffer = new Uint8Array(this._remainingBuffer.subarray(bytesToUse));
        }

        // If we still need more data, read from the stream
        try {
            while (bytesCollected < sliceSize) {
                const { done, value } = await this._reader.read();
                
                if (done) {
                    // If we hit the end before getting enough data, that's an error
                    if (bytesCollected < sliceSize) {
                        throw new Error(`Unexpected end of stream at ${this._currentPosition + bytesCollected}`);
                    }
                    break;
                }

                const remainingNeeded = sliceSize - bytesCollected;

                if (value.length <= remainingNeeded) {
                    // If this chunk fits entirely in our slice, use it all
                    resultBuffer.set(value, bytesCollected);
                    bytesCollected += value.length;
                } else {
                    // If this chunk is too big, use what we need and store the rest
                    resultBuffer.set(value.subarray(0, remainingNeeded), bytesCollected);
                    bytesCollected += remainingNeeded;
                   
                    this._remainingBuffer = new Uint8Array(value.subarray(remainingNeeded));
                }
            }
        } catch (error) {
            console.error('Error reading stream:', error);
            throw error;
        }

        // Update our position
        this._currentPosition += sliceSize;

        return new Blob([resultBuffer]);
    }


    async takeWhole() {
        // Pre-allocate buffer of known size
        const buffer = new Uint8Array(this._uncompressedFileSize);
        let offset = 0;

        try {
            while (true) {
                const { done, value } = await this._reader.read();
                
                if (done) {
                    break;
                }
    
                // Copy new chunk to the pre-allocated buffer at current offset
                buffer.set(value, offset);
                offset += value.length;
            }

            // Verify we got expected amount of data
            if (offset !== this._uncompressedFileSize) {
                console.warn(`Decompressed size (${offset}) doesn't match expected size (${this._uncompressedFileSize})`);
            }

            return new Blob([buffer]);
        } finally {
            this._reader.releaseLock();
        }
    }
}