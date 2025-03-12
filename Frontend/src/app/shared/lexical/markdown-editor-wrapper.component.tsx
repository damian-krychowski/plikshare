import {
    AfterViewInit,
    Component,
    ElementRef,
    input,
    output,
    OnChanges,
    OnDestroy,
    SimpleChanges,
    ViewChild,
    ViewEncapsulation
} from "@angular/core";
import * as React from "react";
import { Root, createRoot } from 'react-dom/client';
import { MarkdownEditor } from "./MarkdownEditor";

const containerElementRef = "customReactComponentContainer";

@Component({
    selector: "app-markdown-editor",
    standalone: true,
    template: `<span #${containerElementRef}></span>`,
    styleUrls: ["./markdown-editor.scss"],
    encapsulation: ViewEncapsulation.None,
})
export class MarkdownEditorWrapperComponent implements OnDestroy, AfterViewInit, OnChanges {
    @ViewChild(containerElementRef, { static: true }) containerRef!: ElementRef;

    // Input/Output signals
    markdownRaw = input<string>('');
    markdownChange = output<string>();
    placeholder = input<string | null | undefined>(null);
    isReadOnly = input(false);

    private reactRoot: Root | null = null;
    private _wasInitialized = false;

    ngOnChanges(changes: SimpleChanges): void {
        if (!this._wasInitialized) return;
        this.render();
    }

    ngAfterViewInit() {
        this.reactRoot = createRoot(this.containerRef.nativeElement);
        this.render();
        this._wasInitialized = true;
    }

    ngOnDestroy() {
        this.reactRoot?.unmount();
    }

    private handleChange = (content: string) => {
        this.markdownChange.emit(content);
    };

    private render() {
        this.reactRoot?.render(
            <React.StrictMode>
                <MarkdownEditor
                    placeholder={this.placeholder()}
                    markdown={this.markdownRaw()}
                    isReadOnly={this.isReadOnly()}
                    onChange={this.handleChange}
                />
            </React.StrictMode>
        );
    }
}