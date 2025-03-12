import React, { FunctionComponent, useEffect, useRef } from "react";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { LexicalComposer } from "@lexical/react/LexicalComposer";
import { PlainTextPlugin } from "@lexical/react/LexicalPlainTextPlugin";
import { ContentEditable } from "@lexical/react/LexicalContentEditable";
import { HistoryPlugin } from "@lexical/react/LexicalHistoryPlugin";
import { LexicalErrorBoundary } from "@lexical/react/LexicalErrorBoundary";
import { $getRoot, $createTextNode, $createParagraphNode, LexicalEditor } from "lexical";

export interface IPlaceholderProps {
    placeholderValue: string;
}

function Placeholder(props: IPlaceholderProps) {
    return <div className="editor-placeholder">{props.placeholderValue}</div>;
}

export interface IMarkdownEditorProps {
    placeholder: string | null | undefined;
    markdown: string | null | undefined;
    isReadOnly: boolean;
    onChange?: (markdown: string) => void;
}

const editorTheme = {
    paragraph: "markdown-line",
    text: {
        code: "markdown-code"
    },
    placeholder: "editor-placeholder"
};

const editorConfig = {
    theme: editorTheme,
    onError(error: any) {
        throw error;
    },
    namespace: "Markdown Editor"
};

function EditorContent(props: IMarkdownEditorProps) {
    const [editor] = useLexicalComposerContext();
    const editorRef = useRef<LexicalEditor | null>(null);

    editorRef.current = editor;

    const setContent = (markdown: string) => {
        editor.update(() => {
            const root = $getRoot();
            root.clear();
            
            const paragraph = $createParagraphNode();
            const text = $createTextNode(markdown);
            paragraph.append(text);
            
            root.append(paragraph);
        });
    };

    const getContent = (): string => {
        let content = "";
        
        editor.getEditorState().read(() => {
            content = $getRoot().getTextContent();
        });

        return content;
    };

    useEffect(() => {
        editor.setEditable(!props.isReadOnly);

        if (!props.isReadOnly) {
            setTimeout(() => editor.focus());
        }
    }, [editor, props.isReadOnly]);

    useEffect(() => {
        if (props.markdown !== undefined && props.markdown !== null) {
            const currentContent = getContent();
            if (currentContent !== props.markdown) {
                setContent(props.markdown);
            }
        }
    }, [props.markdown]);

    useEffect(() => {
        if (!props.onChange) return;

        const removeUpdateListener = editor.registerUpdateListener(() => {
            const content = getContent();
            props.onChange?.(content);
        });

        return () => {
            removeUpdateListener();
        };
    }, [editor, props.onChange]);

    return (
        <div className="editor-inner">
            <PlainTextPlugin
                contentEditable={<ContentEditable className="editor-input font-mono" />}
                placeholder={<Placeholder placeholderValue={props.placeholder ?? 'Enter markdown...'} />}
                ErrorBoundary={LexicalErrorBoundary}
            />
            <HistoryPlugin />
        </div>
    );
}

export const MarkdownEditor: FunctionComponent<IMarkdownEditorProps> = (props: IMarkdownEditorProps) => {
    return (
        <LexicalComposer initialConfig={{
            ...editorConfig,
            editable: !props.isReadOnly,
        }}>
            <div className="editor-container editor-container--no-border">
                <EditorContent {...props} />
            </div>
        </LexicalComposer>
    );
};