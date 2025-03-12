import { HttpClient, HttpHeaders } from "@angular/common/http";
import { computed, Injectable, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { ApplicationSingUp } from "./general-settings.api";
import { DomSanitizer } from "@angular/platform-browser";

export interface GetEntryPageSettingsResponse {
    applicationSignUp: ApplicationSingUp;
    termsOfServiceFilePath: string | null;
    privacyPolicyFilePath: string | null;
}

@Injectable({
    providedIn: 'root'
})
export class EntryPageService {
    applicationSignUp = signal<ApplicationSingUp>('only-invited-users');
    isSignUpAvailable = computed(() => this.applicationSignUp() == 'everyone');

    termsOfServiceFilePath = signal<string | null>(null);
    isTermsOfServiceAvailable = computed(() => this.termsOfServiceFilePath() != null);
    termsOfServiceSafePath = computed(() => {
        const filePath = this.termsOfServiceFilePath();

        if(!filePath)
            return null;

        const safePath = this
            ._sanitizer
            .bypassSecurityTrustResourceUrl(filePath);

        return safePath;
    });

    privacyPolicyFilePath = signal<string | null>(null);
    isPrivacyPolicyAvailable = computed(() => this.privacyPolicyFilePath() != null);   
    privacyPolicySafePath = computed(() => {
        const filePath = this.privacyPolicyFilePath();

        if(!filePath)
            return null;

        return this._sanitizer.bypassSecurityTrustResourceUrl(filePath);
    });

    isAtLeastOneLegalDocumentAvailable = computed(() => 
        this.isTermsOfServiceAvailable() 
        || this.isPrivacyPolicyAvailable());

    areBothLegalDocumentsAvailable = computed(() => 
        this.isTermsOfServiceAvailable()
        && this.isPrivacyPolicyAvailable());

    constructor( 
        private _sanitizer: DomSanitizer,
        private _http: HttpClient) {        
    }

    async reload() {
        try {
            const result = await this.getEntryPageSettings();

            this.applicationSignUp.set(result.applicationSignUp);
            this.termsOfServiceFilePath.set(result.termsOfServiceFilePath);
            this.privacyPolicyFilePath.set(result.privacyPolicyFilePath);
        } catch (error) {
            console.error(error);
        }
    }

    private async getEntryPageSettings(): Promise<GetEntryPageSettingsResponse> {
        const call = this
            ._http
            .get<GetEntryPageSettingsResponse>(
                `/api/entry-page`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }
}