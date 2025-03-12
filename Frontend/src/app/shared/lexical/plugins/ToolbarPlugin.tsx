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

import React from "react";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { useCallback, useEffect, useRef, useState } from "react";
import { CAN_REDO_COMMAND, CAN_UNDO_COMMAND, REDO_COMMAND, UNDO_COMMAND, SELECTION_CHANGE_COMMAND, FORMAT_TEXT_COMMAND, FORMAT_ELEMENT_COMMAND, $getSelection, $isRangeSelection, $createParagraphNode } from "lexical";
import { $isLinkNode, TOGGLE_LINK_COMMAND } from "@lexical/link";
import { $getSelectionStyleValueForProperty, $isParentElementRTL, $wrapNodes, $isAtNodeEnd } from "@lexical/selection";
import { $getNearestNodeOfType, mergeRegister } from "@lexical/utils";
import { INSERT_ORDERED_LIST_COMMAND, INSERT_UNORDERED_LIST_COMMAND, REMOVE_LIST_COMMAND, $isListNode, ListNode } from "@lexical/list";
import { createPortal } from "react-dom";
import { $createHeadingNode, $createQuoteNode, $isHeadingNode } from "@lexical/rich-text";
import { $isCodeNode, getDefaultCodeLanguage } from "@lexical/code";
import FontSize from './FontSize';

const LowPriority = 1;

const supportedBlockTypes = new Set([
    "paragraph",
    "quote",
    "h1",
    "h2",
    "h3",
    "ul",
    "ol"
]);

const blockTypeToBlockName = {
    h1: "Large Heading",
    h2: "Medium Heading",
    h3: "Small Heading",
    h4: "Heading",
    h5: "Heading",
    ol: "Numbered List",
    paragraph: "Normal",
    quote: "Quote",
    ul: "Bulleted List"
};

function Divider() {
    return <div className="divider" />;
}

function positionEditorElement(editor: any, rect: any) {
    if (rect === null) {
        editor.style.opacity = "0";
        editor.style.top = "-1000px";
        editor.style.left = "-1000px";
    } else {
        editor.style.opacity = "1";
        editor.style.top = `${rect.top + rect.height + window.pageYOffset + 10}px`;
        editor.style.left = `${rect.left + window.pageXOffset - editor.offsetWidth / 2 + rect.width / 2}px`;
    }
}

function FloatingLinkEditor({ editor }: any) {
    const editorRef = useRef(null);
    const inputRef = useRef(null);
    const mouseDownRef = useRef(false);
    const [linkUrl, setLinkUrl] = useState("");
    const [isEditMode, setEditMode] = useState(false);
    const [lastSelection, setLastSelection] = useState(null);

    const updateLinkEditor = useCallback(() => {
        const selection = $getSelection();
        if ($isRangeSelection(selection)) {
            const node = getSelectedNode(selection);
            const parent = node.getParent();
            if ($isLinkNode(parent)) {
                setLinkUrl(parent.getURL());
            } else if ($isLinkNode(node)) {
                setLinkUrl(node.getURL());
            } else {
                setLinkUrl("");
            }
        }
        const editorElem = editorRef.current;
        const nativeSelection = window.getSelection();
        const activeElement = document.activeElement;

        if (editorElem === null) {
            return;
        }

        const rootElement = editor.getRootElement();
        if (
            selection !== null &&
            !nativeSelection?.isCollapsed &&
            rootElement !== null &&
            rootElement.contains(nativeSelection?.anchorNode)
        ) {
            const domRange = nativeSelection?.getRangeAt(0);
            let rect;
            if (nativeSelection?.anchorNode === rootElement) {
                let inner = rootElement;
                while (inner.firstElementChild != null) {
                    inner = inner.firstElementChild;
                }
                rect = inner.getBoundingClientRect();
            } else {
                rect = domRange?.getBoundingClientRect();
            }

            if (!mouseDownRef.current) {
                positionEditorElement(editorElem, rect);
            }
            setLastSelection(selection as any);
        } else if (!activeElement || activeElement.className !== "link-input") {
            positionEditorElement(editorElem, null);
            setLastSelection(null);
            setEditMode(false);
            setLinkUrl("");
        }

        return true;
    }, [editor]);

    useEffect(() => {
        return mergeRegister(
            //@ts-ignore
            editor.registerUpdateListener(({ editorState }) => {
                editorState.read(() => {
                    updateLinkEditor();
                });
            }),

            editor.registerCommand(
                SELECTION_CHANGE_COMMAND,
                () => {
                    updateLinkEditor();
                    return true;
                },
                LowPriority
            )
        );
    }, [editor, updateLinkEditor]);

    useEffect(() => {
        editor.getEditorState().read(() => {
            updateLinkEditor();
        });
    }, [editor, updateLinkEditor]);

    useEffect(() => {
        if (isEditMode && inputRef.current) {
            //@ts-ignore
            inputRef.current.focus();
        }
    }, [isEditMode]);

    return (
        <div ref={editorRef} className="link-editor">
            {isEditMode ? (
                <input
                    ref={inputRef}
                    className="link-input"
                    value={linkUrl}
                    onChange={(event) => {
                        setLinkUrl(event.target.value);
                    }}
                    onKeyDown={(event) => {
                        if (event.key === "Enter") {
                            event.preventDefault();
                            if (lastSelection !== null) {
                                if (linkUrl !== "") {
                                    editor.dispatchCommand(TOGGLE_LINK_COMMAND, linkUrl);
                                }
                                setEditMode(false);
                            }
                        } else if (event.key === "Escape") {
                            event.preventDefault();
                            setEditMode(false);
                        }
                    }}
                />
            ) : (
                <>
                    <div className="link-input">
                        <a href={linkUrl} target="_blank" rel="noopener noreferrer">
                            {linkUrl}
                        </a>
                        <div
                            className="link-edit"
                            role="button"
                            tabIndex={0}
                            onMouseDown={(event) => event.preventDefault()}
                            onClick={() => {
                                setEditMode(true);
                            }}
                        />
                    </div>
                </>
            )}
        </div>
    );
}

function Select({ onChange, className, options, value }: any) {
    return (
        <select className={className} onChange={onChange} value={value}>
            <option hidden={true} value="" />
            {options.map((option: any) => (
                <option key={option} value={option}>
                    {option}
                </option>
            ))}
        </select>
    );
}

function getSelectedNode(selection: any) {
    const anchor = selection.anchor;
    const focus = selection.focus;
    const anchorNode = selection.anchor.getNode();
    const focusNode = selection.focus.getNode();
    if (anchorNode === focusNode) {
        return anchorNode;
    }
    const isBackward = selection.isBackward();
    if (isBackward) {
        return $isAtNodeEnd(focus) ? anchorNode : focusNode;
    } else {
        return $isAtNodeEnd(anchor) ? focusNode : anchorNode;
    }
}

function BlockOptionsDropdownList({
    editor,
    blockType,
    toolbarRef,
    setShowBlockOptionsDropDown
}: any) {
    const dropDownRef = useRef(null);

    useEffect(() => {
        const dropDown = dropDownRef.current as any as HTMLDivElement;
        const toolbar = toolbarRef.current;

        if (dropDown !== null && toolbar !== null) {
            const handle = (event: any) => {
                const target = event.target;

                if (!dropDown.contains(target) && !toolbar.contains(target)) {
                    setShowBlockOptionsDropDown(false);
                }
            };
            document.addEventListener("click", handle);

            return () => {
                document.removeEventListener("click", handle);
            };
        }

        return undefined;
    }, [dropDownRef, setShowBlockOptionsDropDown, toolbarRef]);

    useEffect(() => {
        const toolbar = toolbarRef.current;
        const dropDown = dropDownRef.current;

        if (toolbar !== null && dropDown !== null) {
            const { top, left } = toolbar.getBoundingClientRect();
            const scrollTop = window.scrollY || document.documentElement.scrollTop;
            const scrollLeft = window.scrollX || document.documentElement.scrollLeft;

            //@ts-ignore
            dropDown.style.top = `${top + scrollTop + 40}px`;
            //@ts-ignore
            dropDown.style.left = `${left + scrollLeft}px`;
        }
    }, [dropDownRef, toolbarRef]);

    useEffect(() => {
        const dropDown = dropDownRef.current;
        const toolbar = toolbarRef.current;

        if (dropDown !== null && toolbar !== null) {
            const handle = (event: any) => {
                const target = event.target;

                //@ts-ignore
                if (!dropDown.contains(target) && !toolbar.contains(target)) {
                    setShowBlockOptionsDropDown(false);
                }
            };
            document.addEventListener("click", handle);

            return () => {
                document.removeEventListener("click", handle);
            };
        }

        return undefined;
    }, [dropDownRef, setShowBlockOptionsDropDown, toolbarRef]);

    const formatParagraph = () => {
        if (blockType !== "paragraph") {
            editor.update(() => {
                const selection = $getSelection();

                if ($isRangeSelection(selection)) {
                    $wrapNodes(selection, () => $createParagraphNode());
                }
            });
        }
        setShowBlockOptionsDropDown(false);
    };

    const formatHeading1 = () => {
        if (blockType !== "h1") {
            editor.update(() => {
                const selection = $getSelection();

                if ($isRangeSelection(selection)) {
                    $wrapNodes(selection, () => $createHeadingNode("h1"));
                }
            });
        }
        setShowBlockOptionsDropDown(false);
    };

    const formatHeading2 = () => {
        if (blockType !== "h2") {
            editor.update(() => {
                const selection = $getSelection();

                if ($isRangeSelection(selection)) {
                    $wrapNodes(selection, () => $createHeadingNode("h2"));
                }
            });
        }
        setShowBlockOptionsDropDown(false);
    };

    const formatHeading3 = () => {
        if (blockType !== "h3") {
            editor.update(() => {
                const selection = $getSelection();

                if ($isRangeSelection(selection)) {
                    $wrapNodes(selection, () => $createHeadingNode("h3"));
                }
            });
        }
        setShowBlockOptionsDropDown(false);
    };

    const formatBulletList = () => {
        if (blockType !== "ul") {
            editor.dispatchCommand(INSERT_UNORDERED_LIST_COMMAND);
        } else {
            editor.dispatchCommand(REMOVE_LIST_COMMAND);
        }
        setShowBlockOptionsDropDown(false);
    };

    const formatNumberedList = () => {
        if (blockType !== "ol") {
            editor.dispatchCommand(INSERT_ORDERED_LIST_COMMAND);
        } else {
            editor.dispatchCommand(REMOVE_LIST_COMMAND);
        }
        setShowBlockOptionsDropDown(false);
    };

    const formatQuote = () => {
        if (blockType !== "quote") {
            editor.update(() => {
                const selection = $getSelection();

                if ($isRangeSelection(selection)) {
                    $wrapNodes(selection, () => $createQuoteNode());
                }
            });
        }
        setShowBlockOptionsDropDown(false);
    };

    return (
        <div className="toolbar-dropdown" ref={dropDownRef}>
            <button className="item" onClick={formatParagraph}>
                <i className="icon icon-lg icon-lucide-text-normal"></i>  
                <span className="text">Normal</span>
                {blockType === "paragraph" && <span className="active" />}
            </button>
            <button className="item" onClick={formatHeading1}>
                <i className="icon icon-lg icon-lucide-heading-1"></i>  
                <span className="text">Large Heading</span>
                {blockType === "h1" && <span className="active" />}
            </button>
            <button className="item" onClick={formatHeading2}>
                <i className="icon icon-lg icon-lucide-heading-2"></i>  
                <span className="text">Medium Heading</span>
                {blockType === "h2" && <span className="active" />}
            </button>
            <button className="item" onClick={formatHeading3}>
                <i className="icon icon-lg icon-lucide-heading-3"></i>  
                <span className="text">Small Heading</span>
                {blockType === "h3" && <span className="active" />}
            </button>
            <button className="item" onClick={formatBulletList}>
                <i className="icon icon-thin icon-lg icon-lucide-unordered-list"></i>  
                <span className="text">Bullet List</span>
                {blockType === "ul" && <span className="active" />}
            </button>
            <button className="item" onClick={formatNumberedList}>
                <i className="icon icon-lg icon-lucide-ordered-list"></i>  
                <span className="text">Numbered List</span>
                {blockType === "ol" && <span className="active" />}
            </button>
            <button className="item" onClick={formatQuote}>
                <i className="icon icon-lg icon-lucide-message-quote"></i>  
                <span className="text">Quote</span>
                {blockType === "quote" && <span className="active" />}
            </button>
        </div>
    );
}

interface ToolbarPluginProps {
    isFloating?: boolean;
}


export default function ToolbarPlugin({ isFloating = false }: ToolbarPluginProps) {
    const [editor] = useLexicalComposerContext();
    const toolbarRef = useRef(null);
    const [canUndo, setCanUndo] = useState(false);
    const [canRedo, setCanRedo] = useState(false);
    const [blockType, setBlockType] = useState("paragraph");
    const [selectedElementKey, setSelectedElementKey] = useState(null);
    const [showBlockOptionsDropDown, setShowBlockOptionsDropDown] = useState(
        false
    );
    const [codeLanguage, setCodeLanguage] = useState("");
    const [isRTL, setIsRTL] = useState(false);
    const [isLink, setIsLink] = useState(false);
    const [isBold, setIsBold] = useState(false);
    const [isItalic, setIsItalic] = useState(false);
    const [isUnderline, setIsUnderline] = useState(false);
    const [isStrikethrough, setIsStrikethrough] = useState(false);
    const [isCode, setIsCode] = useState(false);
    const [fontSize, setFontSize] = useState<string>('15px');
    const [isEditorFocused, setIsEditorFocused] = useState(false);
    const [isTextSelected, setIsTextSelected] = useState(false);

    const updateToolbar = useCallback(() => {
        const selection = $getSelection();
        if ($isRangeSelection(selection)) {
            const anchorNode = selection.anchor.getNode();
            const element =
                anchorNode.getKey() === "root"
                    ? anchorNode
                    : anchorNode.getTopLevelElementOrThrow();
            const elementKey = element.getKey();
            const elementDOM = editor.getElementByKey(elementKey);
            if (elementDOM !== null) {
                setSelectedElementKey(elementKey as any);
                if ($isListNode(element)) {
                    const parentList = $getNearestNodeOfType(anchorNode, ListNode);
                    const type = parentList ? parentList.getTag() : element.getTag();
                    setBlockType(type);
                } else {
                    const type = $isHeadingNode(element)
                        ? element.getTag()
                        : element.getType();
                    setBlockType(type);
                    if ($isCodeNode(element)) {
                        setCodeLanguage(element.getLanguage() || getDefaultCodeLanguage());
                    }
                }
            }

            setIsBold(selection.hasFormat("bold"));
            setIsItalic(selection.hasFormat("italic"));
            setIsUnderline(selection.hasFormat("underline"));
            setIsStrikethrough(selection.hasFormat("strikethrough"));
            setIsCode(selection.hasFormat("code"));
            setIsRTL($isParentElementRTL(selection));

            const node = getSelectedNode(selection);
            const parent = node.getParent();
            if ($isLinkNode(parent) || $isLinkNode(node)) {
                setIsLink(true);
            } else {
                setIsLink(false);
            }
            setFontSize(
                $getSelectionStyleValueForProperty(selection, 'font-size', '15px'),
            );
        }
    }, [editor]);

    useEffect(() => {
        return mergeRegister(
            editor.registerUpdateListener(({ editorState }) => {
                editorState.read(() => {
                    updateToolbar();
                });
            }),
            editor.registerCommand(
                SELECTION_CHANGE_COMMAND,
                (_payload, newEditor) => {
                    updateToolbar();
                    return false;
                },
                LowPriority
            ),
            editor.registerCommand(
                CAN_UNDO_COMMAND,
                (payload) => {
                    setCanUndo(payload);
                    return false;
                },
                LowPriority
            ),
            editor.registerCommand(
                CAN_REDO_COMMAND,
                (payload) => {
                    setCanRedo(payload);
                    return false;
                },
                LowPriority
            )
        );
    }, [editor, updateToolbar]);

    const updateToolbarPosition = useCallback(() => {
        if (!isFloating) return;

        editor.getEditorState().read(() => {
            const selection = $getSelection();
            const editorElement = editor.getRootElement();
            const toolbar = toolbarRef.current as any as HTMLDivElement;
    
            if (!selection || !editorElement || !toolbar) return;
    
            if (!$isRangeSelection(selection)) {
                setIsTextSelected(false);
                return;
            }
    
            const nativeSelection = window.getSelection();
            if (!nativeSelection || nativeSelection.rangeCount === 0) return;
    
            const domRange = nativeSelection.getRangeAt(0);
            const rangeRect = domRange.getBoundingClientRect();
            const editorRect = editorElement.getBoundingClientRect();
    
            const toolbarRect = toolbar.getBoundingClientRect();
            const toolbarHeight = toolbarRect.height;
            
            const top = rangeRect.top - editorRect.top - toolbarHeight - 10;
            
            const left = Math.max(
                0,  
                Math.min(
                    rangeRect.left - editorRect.left + (rangeRect.width - toolbarRect.width) / 2,
                    editorRect.width - toolbarRect.width  
                )
            );
    
            toolbar.style.top = `${top}px`;
            toolbar.style.left = `${left}px`;
            setIsTextSelected(true);
        });
    }, [editor]);

    useEffect(() => {
        if (!isFloating) return;

        return mergeRegister(
            editor.registerUpdateListener(({ editorState }) => {
                editorState.read(() => {
                    const selection = $getSelection();
                    
                    if (!$isRangeSelection(selection) || selection.isCollapsed()) {
                        setIsTextSelected(false);
                    } else {
                        updateToolbarPosition();
                    }
                });
            }),
            editor.registerCommand(
                SELECTION_CHANGE_COMMAND,
                () => {
                    const selection = $getSelection();
                    if (!$isRangeSelection(selection) || selection.isCollapsed()) {
                        setIsTextSelected(false);
                    } else {
                        updateToolbarPosition();
                    }
                    return false;
                },
                LowPriority
            )
        );
    }, [editor, updateToolbarPosition]);

    
    useEffect(() => {
        if (!isFloating) return;

        const handleBlur = () => {
            requestAnimationFrame(() => {
                const activeElement = document.activeElement;
                const editorElement = editor.getRootElement();
                const toolbar = toolbarRef.current as any as HTMLDivElement;
                
                if (
                    toolbar && 
                    (!activeElement || 
                     (activeElement !== editorElement && 
                      !toolbar.contains(activeElement)))
                ) {
                    setIsEditorFocused(false);
                }
            });
        };

        const editorElement = editor.getRootElement();
        if (editorElement) {
            editorElement.addEventListener('blur', handleBlur, true);
            editorElement.addEventListener('focus', () => setIsEditorFocused(true), true);
        }

        return () => {
            if (editorElement) {
                editorElement.removeEventListener('blur', handleBlur, true);
                editorElement.removeEventListener('focus', () => setIsEditorFocused(true), true);
            }
        };
    }, [editor]);

    const insertLink = useCallback(() => {
        if (!isLink) {
            editor.dispatchCommand(TOGGLE_LINK_COMMAND, "https://");
        } else {
            editor.dispatchCommand(TOGGLE_LINK_COMMAND, null);
        }
    }, [editor, isLink]);

    return (
        <div className={`toolbar ${
                isFloating 
                    ? (isTextSelected && isEditorFocused ? 'toolbar-visible' : 'toolbar-hidden')
                    : 'toolbar-fixed' 
            }`}  ref={toolbarRef}  onMouseDown={(e) => {
                
            e.preventDefault();
        }}>
            <button
                disabled={!canUndo}
                onClick={() => {
                    //@ts-ignore
                    editor.dispatchCommand(UNDO_COMMAND);
                }}
                className="toolbar-item spaced"
                aria-label="Undo">
                <i className="icon icon-lg icon-lucide-undo"></i>  
            </button>

            <button
                disabled={!canRedo}
                onClick={() => {
                    //@ts-ignore
                    editor.dispatchCommand(REDO_COMMAND);
                }}
                className="toolbar-item"
                aria-label="Redo">
                <i className="icon icon-lg icon-lucide-redo"></i>  
            </button>

            <Divider />

            {supportedBlockTypes.has(blockType) && (
                <>
                    <button
                        className="toolbar-item block-controls"
                        onClick={() =>
                            setShowBlockOptionsDropDown(!showBlockOptionsDropDown)
                        }
                        aria-label="Formatting Options">
                        <span className={"icon block-type " + blockType} />
                        <span className="text">{
                            //@ts-ignore
                            blockTypeToBlockName[blockType]
                        }</span>
                        <i className="chevron-down" />
                    </button>
                    {showBlockOptionsDropDown &&
                        createPortal(
                            <BlockOptionsDropdownList
                                editor={editor}
                                blockType={blockType}
                                toolbarRef={toolbarRef}
                                setShowBlockOptionsDropDown={setShowBlockOptionsDropDown}/>,

                            document.body
                        )}
                    <Divider />
                </>
            )}

            <FontSize
                selectionFontSize={fontSize.slice(0, -2)}
                editor={editor} />

            <Divider />
            
            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_TEXT_COMMAND, "bold");
                }}
                className={"toolbar-item spaced " + (isBold ? "active" : "")}
                aria-label="Format Bold">
                <i className="icon icon-lg icon-lucide-text-bold"></i>  
            </button>

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_TEXT_COMMAND, "italic");
                }}
                className={"toolbar-item spaced " + (isItalic ? "active" : "")}
                aria-label="Format Italics">
                <i className="icon icon-thin icon-lg icon-lucide-text-italic"></i>  
            </button>

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_TEXT_COMMAND, "underline");
                }}
                className={"toolbar-item spaced " + (isUnderline ? "active" : "")}
                aria-label="Format Underline">
                <i className="icon icon-lg icon-lucide-text-underline"></i>  
            </button>

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_TEXT_COMMAND, "strikethrough");
                }}
                className={
                    "toolbar-item spaced " + (isStrikethrough ? "active" : "")
                }
                aria-label="Format Strikethrough">
                <i className="icon icon-lg icon-lucide-text-strikethrough"></i>  
            </button>

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_TEXT_COMMAND, "code");
                }}
                className={"toolbar-item spaced " + (isCode ? "active" : "")}
                aria-label="Insert Code">
                <i className="icon icon-thin icon-lg icon-lucide-code"></i>  
            </button>

            <button
                onClick={insertLink}
                className={"toolbar-item spaced " + (isLink ? "active" : "")}
                aria-label="Insert Link">
                <i className="icon icon-thin icon-lg icon-lucide-text-link"></i>  
            </button>
            {isLink &&
                createPortal(<FloatingLinkEditor editor={editor} />, document.body)}

            <Divider />

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_ELEMENT_COMMAND, "left");
                }}
                className="toolbar-item spaced"
                aria-label="Left Align">
                <i className="icon icon-lg icon-lucide-text-align-left"></i>  
            </button>

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_ELEMENT_COMMAND, "center");
                }}
                className="toolbar-item spaced"
                aria-label="Center Align">
                <i className="icon icon-lg icon-lucide-text-align-center"></i>  
            </button>

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_ELEMENT_COMMAND, "right");
                }}
                className="toolbar-item spaced"
                aria-label="Right Align">
                <i className="icon icon-lg icon-lucide-text-align-right"></i>  
            </button>

            <button
                onClick={() => {
                    editor.dispatchCommand(FORMAT_ELEMENT_COMMAND, "justify");
                }}
                className="toolbar-item"
                aria-label="Justify Align">
                <i className="icon icon-lg icon-lucide-text-align-justify"></i>  
            </button>
        </div>
    );
}
