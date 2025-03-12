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

import React from 'react';
import { AutoLinkPlugin } from "@lexical/react/LexicalAutoLinkPlugin";

const URL_MATCHER: RegExp = /((https?:\/\/(www\.)?)|(www\.))[-a-zA-Z0-9@:%._+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_+.~#?&//=]*)/;
const EMAIL_MATCHER: RegExp = /(([^<>()[\]\\.,;:\s@"]+(\.[^<>()[\]\\.,;:\s@"]+)*)|(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))/;

interface MatcherReturn {
  index: number;
  length: number;
  text: string;
  url: string;
}

const MATCHERS: ((text: string) => MatcherReturn | null)[] = [
  (text: string): MatcherReturn | null => {
    const match = URL_MATCHER.exec(text);
    return (
      match && {
        index: match.index,
        length: match[0].length,
        text: match[0],
        url: match[0],
      }
    );
  },
  (text: string): MatcherReturn | null => {
    const match = EMAIL_MATCHER.exec(text);
    return (
      match && {
        index: match.index,
        length: match[0].length,
        text: match[0],
        url: `mailto:${match[0]}`,
      }
    );
  },
];

export default function PlaygroundAutoLinkPlugin(): React.ReactElement  {
  return <AutoLinkPlugin matchers={MATCHERS} />;
}
