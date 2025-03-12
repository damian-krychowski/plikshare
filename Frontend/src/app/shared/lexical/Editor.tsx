/*
* Original work Copyright (c) Meta Platforms, Inc. and affiliates.
* Released under MIT License
* https://github.com/facebook/lexical/blob/main/LICENSE
*
* This file is based on Lexical's React Rich Text Editor example:
* https://github.com/facebook/lexical/tree/main/packages/lexical-playground
*
* Modified work Copyright (c) 2025 Damian Krychowski
* Released under AGPLv3
*/

import React, { FunctionComponent, useEffect, useRef } from "react";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { LexicalComposer } from "@lexical/react/LexicalComposer";
import { RichTextPlugin } from "@lexical/react/LexicalRichTextPlugin";
import { ContentEditable } from "@lexical/react/LexicalContentEditable";
import { HistoryPlugin } from "@lexical/react/LexicalHistoryPlugin";
import { AutoFocusPlugin } from "@lexical/react/LexicalAutoFocusPlugin";
import { LexicalErrorBoundary } from "@lexical/react/LexicalErrorBoundary";
import { HeadingNode, QuoteNode } from "@lexical/rich-text";
import { TableCellNode, TableNode, TableRowNode } from "@lexical/table";
import { ListItemNode, ListNode } from "@lexical/list";
import { CodeHighlightNode, CodeNode } from "@lexical/code";
import { AutoLinkNode, LinkNode } from "@lexical/link";
import { LinkPlugin } from "@lexical/react/LexicalLinkPlugin";
import { ListPlugin } from "@lexical/react/LexicalListPlugin";

import ListMaxIndentLevelPlugin from "./plugins/ListMaxIndentLevelPlugin";
import AutoLinkPlugin from "./plugins/AutoLinkPlugin";
import ToolbarPlugin from "./plugins/ToolbarPlugin";
import { $getRoot, EditorState, LexicalEditor } from "lexical";
import { $generateHtmlFromNodes } from '@lexical/html';
import { $convertFromMarkdownString, $convertToMarkdownString, TRANSFORMERS } from "@lexical/markdown";

export interface IPlaceholderProps {
    placeholderValue: string;
}

function Placeholder(props: IPlaceholderProps) {
    return <div className="editor-placeholder">{props.placeholderValue}</div>;
}

export type LexicalSaveState = {
    json: string;
    html: string;
}

export interface ILexicalEditorProps {
    isToolbarFloating: boolean;
    placeholder: string | null | undefined;
    savedState: string | null | undefined;
    savedMarkdowState: string | null | undefined;
    isReadOnly: boolean;

    onEditorStateChange: (state: EditorState) => void;
    onGetHtmlAndJson: (getHtml: () => LexicalSaveState) => void;
    onGetMarkdown: (getMarkdown: () => string) => void;
    onReset: (reset: () => void) => void;
    onSetState: (setState: (json: string | null | undefined) => void) => void;
    onSetStateFromMarkdown: (setStateFromMarkdown: (markdown: string | null | undefined) => void) => void;
}

const plikShareTheme = {
    ltr: "editor-ltr",
    rtl: "editor-rtl",
    placeholder: "editor-placeholder",
    paragraph: "editor-paragraph",
    quote: "editor-quote",
    heading: {
        h1: "editor-heading-h1",
        h2: "editor-heading-h2",
        h3: "editor-heading-h3",
        h4: "editor-heading-h4",
        h5: "editor-heading-h5"
    },
    list: {
        nested: {
            listitem: "editor-nested-listitem"
        },
        ol: "editor-list-ol",
        ul: "editor-list-ul",
        listitem: "editor-listitem"
    },
    image: "editor-image",
    link: "editor-link",
    text: {
        bold: "editor-text-bold",
        italic: "editor-text-italic",
        overflowed: "editor-text-overflowed",
        hashtag: "editor-text-hashtag",
        underline: "editor-text-underline",
        strikethrough: "editor-text-strikethrough",
        underlineStrikethrough: "editor-text-underlineStrikethrough",
        code: "editor-text-code"
    },
    code: "editor-code",
    codeHighlight: {
        atrule: "editor-tokenAttr",
        attr: "editor-tokenAttr",
        boolean: "editor-tokenProperty",
        builtin: "editor-tokenSelector",
        cdata: "editor-tokenComment",
        char: "editor-tokenSelector",
        class: "editor-tokenFunction",
        "class-name": "editor-tokenFunction",
        comment: "editor-tokenComment",
        constant: "editor-tokenProperty",
        deleted: "editor-tokenProperty",
        doctype: "editor-tokenComment",
        entity: "editor-tokenOperator",
        function: "editor-tokenFunction",
        important: "editor-tokenVariable",
        inserted: "editor-tokenSelector",
        keyword: "editor-tokenAttr",
        namespace: "editor-tokenVariable",
        number: "editor-tokenProperty",
        operator: "editor-tokenOperator",
        prolog: "editor-tokenComment",
        property: "editor-tokenProperty",
        punctuation: "editor-tokenPunctuation",
        regex: "editor-tokenVariable",
        selector: "editor-tokenSelector",
        string: "editor-tokenSelector",
        symbol: "editor-tokenProperty",
        tag: "editor-tokenProperty",
        url: "editor-tokenOperator",
        variable: "editor-tokenVariable"
    }
};

const editorConfig = {
    theme: plikShareTheme,
    onError(error: any) {
        throw error;
    },
    nodes: [
        HeadingNode,
        ListNode,
        ListItemNode,
        QuoteNode,
        CodeNode,
        CodeHighlightNode,
        TableNode,
        TableCellNode,
        TableRowNode,
        AutoLinkNode,
        LinkNode
    ],
    namespace: "Lexical Editor"
};

function EditorContent(props: ILexicalEditorProps) {
    const [editor] = useLexicalComposerContext();
    const editorRef = useRef<LexicalEditor | null>(null);

    editorRef.current = editor;

    const resetEditor = () => {
        editor.update(() => {
            const root = $getRoot();
            root.clear();
        });
    };

    const setEditorState = (json: string | null | undefined) => {
        if(!json) {
            resetEditor();
        } else {        
            const parsedJson = JSON.parse(json);
            const parsedState = editor.parseEditorState(parsedJson);
            editor.setEditorState(parsedState);
        }
    };

    const setEditorStateFromMarkdown = (markdown: string | null | undefined) => {
        if(!markdown) {
            resetEditor();
        } else {        
            editor.update(() => {
                $convertFromMarkdownString(markdown, TRANSFORMERS);
            });
        }
    }

    const getHtml = (): LexicalSaveState => {
        let html = "";
        let json = "";

        editorRef.current?.update(() => {
            const editorState = editorRef
                .current
                ?.getEditorState();

            editorState?.read(() => {
                const lexicalEditor: LexicalEditor = editorRef.current as LexicalEditor;

                html = $generateHtmlFromNodes(lexicalEditor); 
                json = JSON.stringify(editorState.toJSON());
            });
        });

        return {
            json,
            html
        };
    };

    const getMarkdown = (): string => {
        let markdown = "";

        editorRef.current?.update(() => {
            markdown = $convertToMarkdownString(TRANSFORMERS);
        });

        return markdown;
    };

    useEffect(() => {
        const loadSavedState = (jsonString: string | null) => {
            if (!jsonString) {
                return;
            }
            const parsedJson = JSON.parse(jsonString);
            const parsedState = editor.parseEditorState(parsedJson);
            editor.setEditorState(parsedState);
        };

        const loadMarkdownState = (markdown: string | null) => {
            if(!markdown) {
                return;
            }

            editor.update(() => {
                $convertFromMarkdownString(markdown, TRANSFORMERS);
            });
        };

        if (props.savedState) {
            loadSavedState(props.savedState);
        } else if (props.savedMarkdowState) {
            loadMarkdownState(props.savedMarkdowState);
        }

    }, [editor, props.savedState]);

    useEffect(() => {
        if (props.onGetMarkdown) {
            props.onGetMarkdown(getMarkdown);
        }
    }, [props, getMarkdown]);

    useEffect(() => {
        if (props.onGetHtmlAndJson) {
            props.onGetHtmlAndJson(getHtml);
        }
    }, [props, getHtml]);

    useEffect(() => {
        if (props.onReset) {
            props.onReset(resetEditor);
        }
    }, [props.onReset]);

    useEffect(() => {
        if (props.onSetState) {
            props.onSetState(setEditorState);
        }
    }, [props.onSetState]);

    useEffect(() => {
        if (props.onSetStateFromMarkdown) {
            props.onSetStateFromMarkdown(setEditorStateFromMarkdown);
        }
    }, [props.onSetStateFromMarkdown]);

    useEffect(() => {
        editor.setEditable(!props.isReadOnly);

        if(!props.isReadOnly) {
            setTimeout(() => editor.focus());
        }
    }, [editor, props.isReadOnly]);

    useEffect(() => {
        const removeUpdateListener = editor.registerUpdateListener(({ editorState }) => {
            props.onEditorStateChange?.(editorState);
        });

        return () => {
            removeUpdateListener();
        };
    }, [editor]);

    return (
        <>
            {!props.isReadOnly && <ToolbarPlugin isFloating={props.isToolbarFloating}/>}
            <div className="editor-inner">
                <RichTextPlugin
                    contentEditable={<ContentEditable className="editor-input" />}
                    placeholder={<Placeholder placeholderValue={props.placeholder ?? 'Enter some rich text...'}  />}
                    ErrorBoundary={LexicalErrorBoundary}
                />
                <HistoryPlugin />
                <AutoFocusPlugin />
                <ListPlugin />
                <LinkPlugin />
                <AutoLinkPlugin />
                <ListMaxIndentLevelPlugin maxDepth={7} />
            </div>
        </>
    );
}

export const Editor: FunctionComponent<ILexicalEditorProps> = (props: ILexicalEditorProps) => {
    return (
        <LexicalComposer initialConfig={{
            ...editorConfig,
            editable: !props.isReadOnly,
        }}>
            <div className="editor-container">
                <EditorContent
                    placeholder={props.placeholder}
                    isToolbarFloating={props.isToolbarFloating}
                    onEditorStateChange={props.onEditorStateChange}
                    onGetHtmlAndJson={props.onGetHtmlAndJson}
                    onGetMarkdown={props.onGetMarkdown}
                    savedState={props.savedState}
                    savedMarkdowState={props.savedMarkdowState}
                    onReset={props.onReset}
                    onSetState={props.onSetState}
                    onSetStateFromMarkdown={props.onSetStateFromMarkdown}
                    isReadOnly={props.isReadOnly}
                 />
            </div>
        </LexicalComposer>
    );
}