import {
    AfterViewInit, Component, ElementRef, Input, input, 
    OnChanges, 
    OnDestroy, output, SimpleChanges, ViewChild, ViewEncapsulation
} from "@angular/core";
import * as React from "react";
import { Root, createRoot } from 'react-dom/client';
import { Editor, LexicalSaveState } from "./Editor";
import { EditorState, LexicalEditor } from "lexical";

export type LexicalEditorChanges = {
    editorState: EditorState;
    editor: LexicalEditor;
};

export const LEXICAL_EMPTY_JSON =  '{"root":{"children":[{"children":[],"direction":null,"format":"","indent":0,"type":"paragraph","version":1}],"direction":null,"format":"","indent":0,"type":"root","version":1}}';
export const LEXICAL_EMPTY_AFTER_RESET = '{"root":{"children":[],"direction":null,"format":"","indent":0,"type":"root","version":1}}';

const containerElementRef = "customReactComponentContainer";

@Component({
    selector: "app-lexical-editor",
    standalone: true,
    template: `<span #${containerElementRef}></span>`,
    styleUrls: ["./lexical-editor.scss"],
    encapsulation: ViewEncapsulation.None,
})
export class LexicalEditorWrapperComponent implements OnDestroy, AfterViewInit, OnChanges {
    @ViewChild(containerElementRef, { static: true }) containerRef!: ElementRef;

    editorStateChange = output<EditorState>();
    savedJsonState = input.required<string | null | undefined>();
    savedMarkdownState = input<string | null | undefined>(null);
    isToolbarFloating = input.required<boolean>();
    placeholder = input<string | null | undefined>(null);
    isReadOnly = input(false);

    private reactRoot: Root | null = null;
    private getMarkdown: () => string = () => '';
    private getHtmlAndJson: () => LexicalSaveState = () => ({ json: "", html: "", markdown: "" });
    private reset: () => void = () => {};
    private setState: (json: string | null | undefined) => void = (_) => {};
    private setStateFromMarkdown: (markdown: string | null | undefined) => void = (_) => {};

    private _wasInitialized = false;
    private _currentJson: string | null = null;

    constructor() {
        this.handleEditorStateChange = this.handleEditorStateChange.bind(this);
    }

    ngOnChanges(changes: SimpleChanges): void {
        if(!this._wasInitialized)
            return;

        if(changes['isReadOnly']) {
            this.render();
        } 

        if(changes['savedJsonState'] && this.savedJsonState() !== this._currentJson) {
            this.setState(this.savedJsonState())
        }

        if(changes['savedMarkdownState'] && this.savedMarkdownState() != this.getMarkdownState()) {
            this.setStateFromMarkdown(this.savedMarkdownState());
        }
    }

    ngAfterViewInit() {
        this.reactRoot = createRoot(
            this.containerRef.nativeElement);

        this.render();
        this._wasInitialized = true;
    }

    public handleEditorStateChange(state: EditorState) {
        if (this.editorStateChange) {
            this._currentJson = JSON.stringify(state.toJSON());
            this.editorStateChange.emit(state);
        }
    }

    ngOnDestroy() {
        this.reactRoot?.unmount();
    }

    public setGetHtmlAndJsonMethod = (getHtmlAndJsonMethod: () => LexicalSaveState) => {
        this.getHtmlAndJson = getHtmlAndJsonMethod;
    }

    public setGetMarkdownMethod = (getMarkdownMethod: () => string) => {
        this.getMarkdown = getMarkdownMethod;
    }

    public setResetMethod = (reset: () => void) => {
        this.reset = reset;
    }

    public setSetStateMethod = (setState: (json: string | null | undefined) => void) => {
        this.setState = setState;
    }

    public setSetStateFromMarkdownMethod = (setStateFromMarkdown: (markdown: string | null | undefined) => void) => {
        this.setStateFromMarkdown = setStateFromMarkdown;
    }

    public getSaveState(): LexicalSaveState {
        if (!this.getHtmlAndJson) {
            throw new Error("HTML generation function is not set.");
        }

        return this.getHtmlAndJson();
    }

    public getMarkdownState(): string {        
        if (!this.getMarkdown) {
            throw new Error("Markdown generation function is not set.");
        }

        return this.getMarkdown();
    }

    public resetState() {
        this.reset();
    }

    private render() {
        this.reactRoot?.render(
            <React.StrictMode>
                <Editor 
                    placeholder={this.placeholder()}
                    isToolbarFloating={this.isToolbarFloating()}
                    onEditorStateChange={this.handleEditorStateChange}
                    onGetHtmlAndJson={this.setGetHtmlAndJsonMethod}
                    onGetMarkdown={this.setGetMarkdownMethod}
                    savedState={this.savedJsonState()}
                    savedMarkdowState={this.savedMarkdownState()}
                    onReset={this.setResetMethod}
                    onSetState={this.setSetStateMethod}
                    onSetStateFromMarkdown={this.setSetStateFromMarkdownMethod}
                    isReadOnly={this.isReadOnly()}
                />
            </React.StrictMode>
        );
    }
}