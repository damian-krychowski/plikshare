import type { CurrentFolderDto, FileDto, GetFolderResponse, SubfolderDto, UploadDto } from "./folders-and-files.api";

export type FolderContentChunk = {
    folder?: CurrentFolderDto;
    subfolders?: SubfolderDto[];
    files?: FileDto[];
    uploads?: UploadDto[];
    totalFileCount?: number;
}

export type FolderContentStreamObserver = {
    onChunk: (chunk: FolderContentChunk, index: number) => void;
    onCompleted?: () => void;
    onError?: (error: any) => void;
}

export class FolderContentStream {
    private _chunks: FolderContentChunk[] = [];
    private _observers: FolderContentStreamObserver[] = [];
    private _isCompleted = false;
    private _error: any = null;

    private _resolveWhole!: (response: GetFolderResponse) => void;
    private _rejectWhole!: (error: any) => void;

    public readonly whole = new Promise<GetFolderResponse>((resolve, reject) => {
        this._resolveWhole = resolve;
        this._rejectWhole = reject;
    });

    constructor() {
        this.whole.catch(() => {});
    }

    public push(chunk: FolderContentChunk) {
        this._chunks.push(chunk);

        const index = this._chunks.length - 1;

        for (const observer of [...this._observers]) {
            observer.onChunk(chunk, index);
        }
    }

    public complete() {
        this._isCompleted = true;
        this._resolveWhole(this.mergeChunks());

        const observers = this._observers;
        this._observers = [];

        for (const observer of observers) {
            observer.onCompleted?.();
        }
    }

    public fail(error: any) {
        this._error = error;
        this._rejectWhole(error);

        const observers = this._observers;
        this._observers = [];

        for (const observer of observers) {
            observer.onError?.(error);
        }
    }

    public subscribe(observer: FolderContentStreamObserver) {
        this._chunks.forEach((chunk, index) => observer.onChunk(chunk, index));

        if (this._isCompleted) {
            observer.onCompleted?.();
            return;
        }

        if (this._error) {
            observer.onError?.(this._error);
            return;
        }

        this._observers.push(observer);
    }

    private mergeChunks(): GetFolderResponse {
        const response: GetFolderResponse = {
            folder: null!,
            subfolders: [],
            files: [],
            uploads: []
        };

        for (const chunk of this._chunks) {
            if (chunk.folder) {
                response.folder = chunk.folder;
            }

            if (chunk.subfolders) {
                response.subfolders.push(...chunk.subfolders);
            }

            if (chunk.files) {
                response.files.push(...chunk.files);
            }

            if (chunk.uploads) {
                response.uploads.push(...chunk.uploads);
            }
        }

        return response;
    }

    public static fromResponse(response: {
        folder?: CurrentFolderDto | null;
        subfolders?: SubfolderDto[];
        files?: FileDto[];
        uploads?: UploadDto[];
    }): FolderContentStream {
        const stream = new FolderContentStream();

        stream.push({
            folder: response.folder ?? undefined,
            subfolders: response.subfolders ?? [],
            files: response.files ?? [],
            uploads: response.uploads ?? []
        });

        stream.complete();

        return stream;
    }

    public static fromChunkProducer(
        produce: (push: (chunk: FolderContentChunk) => void) => Promise<void>
    ): FolderContentStream {
        const stream = new FolderContentStream();

        produce(chunk => stream.push(chunk))
            .then(
                () => stream.complete(),
                error => stream.fail(error));

        return stream;
    }

    public static fromPromise(promise: Promise<GetFolderResponse>): FolderContentStream {
        const stream = new FolderContentStream();

        promise.then(
            response => {
                stream.push({
                    folder: response.folder ?? undefined,
                    subfolders: response.subfolders ?? [],
                    files: response.files ?? [],
                    uploads: response.uploads ?? []
                });

                stream.complete();
            },
            error => stream.fail(error)
        );

        return stream;
    }
}
