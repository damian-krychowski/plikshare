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
import { $patchStyleText } from '@lexical/selection';
import { $getSelection, LexicalEditor } from 'lexical';

const MIN_ALLOWED_FONT_SIZE = 8;
const MAX_ALLOWED_FONT_SIZE = 72;
const DEFAULT_FONT_SIZE = 15;

// eslint-disable-next-line no-shadow
enum updateFontSizeType {
    increment = 1,
    decrement,
}

export default function FontSize({
    selectionFontSize,
    editor,
}: {
    selectionFontSize: string;
    editor: LexicalEditor;
}) {
    const [inputValue, setInputValue] = React.useState<string>(selectionFontSize);

    /**
     * Calculates the new font size based on the update type.
     * @param currentFontSize - The current font size
     * @param updateType - The type of change, either increment or decrement
     * @returns the next font size
     */
    const calculateNextFontSize = (
        currentFontSize: number,
        updateType: updateFontSizeType | null,
    ) => {
        if (!updateType) {
            return currentFontSize;
        }

        let updatedFontSize: number = currentFontSize;
        switch (updateType) {
            case updateFontSizeType.decrement:
                switch (true) {
                    case currentFontSize > MAX_ALLOWED_FONT_SIZE:
                        updatedFontSize = MAX_ALLOWED_FONT_SIZE;
                        break;
                    case currentFontSize >= 48:
                        updatedFontSize -= 12;
                        break;
                    case currentFontSize >= 24:
                        updatedFontSize -= 4;
                        break;
                    case currentFontSize >= 14:
                        updatedFontSize -= 2;
                        break;
                    case currentFontSize >= 9:
                        updatedFontSize -= 1;
                        break;
                    default:
                        updatedFontSize = MIN_ALLOWED_FONT_SIZE;
                        break;
                }
                break;

            case updateFontSizeType.increment:
                switch (true) {
                    case currentFontSize < MIN_ALLOWED_FONT_SIZE:
                        updatedFontSize = MIN_ALLOWED_FONT_SIZE;
                        break;
                    case currentFontSize < 12:
                        updatedFontSize += 1;
                        break;
                    case currentFontSize < 20:
                        updatedFontSize += 2;
                        break;
                    case currentFontSize < 36:
                        updatedFontSize += 4;
                        break;
                    case currentFontSize <= 60:
                        updatedFontSize += 12;
                        break;
                    default:
                        updatedFontSize = MAX_ALLOWED_FONT_SIZE;
                        break;
                }
                break;

            default:
                break;
        }
        return updatedFontSize;
    };
    /**
     * Patches the selection with the updated font size.
     */

    const updateFontSizeInSelection = React.useCallback(
        (newFontSize: string | null, updateType: updateFontSizeType | null) => {
            const getNextFontSize = (prevFontSize: string | null): string => {
                if (!prevFontSize) {
                    prevFontSize = `${DEFAULT_FONT_SIZE}px`;
                }
                prevFontSize = prevFontSize.slice(0, -2);
                const nextFontSize = calculateNextFontSize(
                    Number(prevFontSize),
                    updateType,
                );
                return `${nextFontSize}px`;
            };

            editor.update(() => {
                if (editor.isEditable()) {
                    const selection = $getSelection();
                    if (selection !== null) {
                        $patchStyleText(selection, {
                            'font-size': newFontSize || getNextFontSize,
                        });
                    }
                }
            });
        },
        [editor],
    );

    const handleKeyPress = (e: React.KeyboardEvent<HTMLInputElement>) => {
        const inputValueNumber = Number(inputValue);

        if (['e', 'E', '+', '-'].includes(e.key) || isNaN(inputValueNumber)) {
            e.preventDefault();
            setInputValue('');
            return;
        }

        if (e.key === 'Enter') {
            e.preventDefault();

            let updatedFontSize = inputValueNumber;
            if (inputValueNumber > MAX_ALLOWED_FONT_SIZE) {
                updatedFontSize = MAX_ALLOWED_FONT_SIZE;
            } else if (inputValueNumber < MIN_ALLOWED_FONT_SIZE) {
                updatedFontSize = MIN_ALLOWED_FONT_SIZE;
            }
            setInputValue(String(updatedFontSize));
            updateFontSizeInSelection(String(updatedFontSize) + 'px', null);
        }
    };

    const handleButtonClick = (updateType: updateFontSizeType) => {
        if (inputValue !== '') {
            const nextFontSize = calculateNextFontSize(
                Number(inputValue),
                updateType,
            );
            updateFontSizeInSelection(String(nextFontSize) + 'px', null);
        } else {
            updateFontSizeInSelection(null, updateType);
        }
    };

    React.useEffect(() => {
        setInputValue(selectionFontSize);
    }, [selectionFontSize]);

    return (
        <>
            <button
                type="button"
                disabled={(selectionFontSize !== '' && Number(inputValue) <= MIN_ALLOWED_FONT_SIZE)}
                onClick={() => handleButtonClick(updateFontSizeType.decrement)}
                className="toolbar-item font-decrement">
                <i className="icon icon-thin icon-lg icon-lucide-text-size-decrease"></i>
            </button>

            <input
                type="text"
                readOnly                
                value={inputValue}
                className="toolbar-item font-size-input"
                min={MIN_ALLOWED_FONT_SIZE}
                max={MAX_ALLOWED_FONT_SIZE}
                onChange={(e) => setInputValue(e.target.value)}
                onKeyDown={handleKeyPress}
            />

            <button
                type="button"
                disabled={(selectionFontSize !== '' && Number(inputValue) >= MAX_ALLOWED_FONT_SIZE)}
                onClick={() => handleButtonClick(updateFontSizeType.increment)}
                className="toolbar-item">
                <i className="icon icon-thin icon-lg icon-lucide-text-size-increase"></i>
            </button>
        </>
    );
}
