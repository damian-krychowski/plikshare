import { filter, Subject, Subscription } from "rxjs";

export type FileInlinePreviewCommandType = 
    'save-content-change'
    | 'cancel-content-change';

export type FileInlinePreviewCommand = 
    SaveContentChangeFileInlinePreviewCommand
    | CancelContentChangeFileInlinePreviewCommand;

export type SaveContentChangeFileInlinePreviewCommand = {
    type: 'save-content-change';
    callback: (content: string, contentType: string) => Promise<void>
}

export type CancelContentChangeFileInlinePreviewCommand = {
    type: 'cancel-content-change';
}

export class FileInlinePreviewCommandsPipeline {
    private commandSubject = new Subject<FileInlinePreviewCommand>();

    public emit(command: FileInlinePreviewCommand) {
        this.commandSubject.next(command);
    }

    public subscribe(command: 'save-content-change', handler: (callback: (content: string, contentType:string) => Promise<void>) => void): Subscription;
    public subscribe(command: 'cancel-content-change', handler: () => void): Subscription;
    public subscribe(command: FileInlinePreviewCommandType, handler: any): Subscription {
        return this.commandSubject.pipe(
            filter((cmd): cmd is FileInlinePreviewCommand => cmd.type === command)
        ).subscribe((cmd) => {
            if (cmd.type === 'save-content-change') {
                handler(cmd.callback);
            } else {
                handler();
            }
        });
    }
}