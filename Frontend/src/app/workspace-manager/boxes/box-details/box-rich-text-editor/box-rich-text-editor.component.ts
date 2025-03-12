import { Component, computed, input, output, signal, ViewChild, WritableSignal } from "@angular/core";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { LexicalEditorWrapperComponent } from "../../../../shared/lexical/lexical-editor-wrapper.component";
import { toggle } from "../../../../shared/signal-utils";
import { MatTooltipModule } from "@angular/material/tooltip";
import { EditorState } from "lexical";
import { Debouncer } from "../../../../services/debouncer";

export type AppBoxRichTextItem = {
    isEnabled: WritableSignal<boolean>;
    json: WritableSignal<string | null>;
    
    operations: AppBoxRichTextOperations;
}

export type AppBoxRichTextOperations = {
    updateIsEnabled: (isEnabled: boolean) => Promise<void>;
    update: (json: string, html: string) => Promise<void>;
}

type RichTextEditorState = 'nothing-changed' | 'saved' | 'typing' | 'saving'

@Component({
    selector: 'app-box-rich-text-editor',
    imports: [
        MatTooltipModule,
        MatSlideToggleModule,
        LexicalEditorWrapperComponent
    ],
    templateUrl: './box-rich-text-editor.component.html',
    styleUrl: './box-rich-text-editor.component.scss'
})
export class BoxRichTextEditorComponent {
    name = input.required<'header' | 'footer'>();
    richText = input.required<AppBoxRichTextItem>();
    isLoadingChanged = output<boolean>();

    isEnabled = computed(() => this.richText().isEnabled());
    json = computed(() => this.richText().json());

    isLoading = signal(false);
    state = signal<RichTextEditorState>('nothing-changed');

    @ViewChild('editor') lexicalEditor: LexicalEditorWrapperComponent | null = null;


    private _savingDebouncer = new Debouncer(1000)
    private _noTypingDebouncer = new Debouncer(500);

    async editorStateChanged(state: EditorState){
        const currentJson = JSON.stringify(state.toJSON());
        const hasJsonChanged = currentJson !== this.json();
                
        if(hasJsonChanged){
            this.state.set('typing');
            this._savingDebouncer.debounce(() => this.save())
        } else {
            this._savingDebouncer.cancel()
        }

        if(this.state() == 'typing'){
            this._noTypingDebouncer.debounce(() => this.state.update(value => {
                if(value == 'saved' || value == 'saving')
                    return value;

                if(this._savingDebouncer.isOn())
                    return 'saving';

                return 'nothing-changed';
            }));
        }
    }

    private async save() {
        const editorSaveState = this.lexicalEditor?.getSaveState();

        if(!editorSaveState)
            return;

        if(editorSaveState.json === this.json()){
            this.state.set('nothing-changed');
            return;
        }

        const richText = this.richText();

        const originalJsonBuffer = richText.json();

        richText.json.set(editorSaveState.json);

        try {
            this.setIsLoading(true);
            this.state.set('saving');

            await richText.operations.update(
                editorSaveState.json, 
                editorSaveState.html);

            this.state.set('saved');
        } catch (error) {
            console.error(error);
            
            richText.json.set(originalJsonBuffer);
        } finally {
            this.setIsLoading(false);
        }        
    }

    async isEnabledChanged() {
        const richText = this.richText();

        try {
            this.setIsLoading(true);

            const isEnabled = toggle(richText.isEnabled);

            await richText.operations.updateIsEnabled(isEnabled);
        } catch(error) {
            console.error(error);
        } finally {
            this.setIsLoading(false);            
        } 
    }

    private setIsLoading(value: boolean) {
        this.isLoading.set(value);
        this.isLoadingChanged.emit(value);
    }
}