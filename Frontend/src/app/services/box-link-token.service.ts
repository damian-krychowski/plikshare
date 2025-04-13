import { Injectable } from '@angular/core';

export const BOX_LINK_TOKEN_HEADER = "X-BOX-LINK-TOKEN";
const BOX_LINK_TOKEN_LOCAL_STORAGE_KEY = "PLIKSHARE_BOX_LINK_TOKEN";

@Injectable({
    providedIn: 'root'
})
export class BoxLinkTokenService {
    public set(token: string) {
        localStorage.setItem(BOX_LINK_TOKEN_LOCAL_STORAGE_KEY, token);
    }        

    public get(): string | undefined {
        const token = localStorage.getItem(BOX_LINK_TOKEN_LOCAL_STORAGE_KEY);

        if(token) return token;
        return undefined;
    }
}