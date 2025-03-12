import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { firstValueFrom } from "rxjs";

export interface SignUpResponse {
    code: 'confirmation-email-sent' | 'invitation-required' | 'signed-up-and-signed-in';
}

export interface ResendConfirmationEmailResponse {
    code: 'confirmation-email-sent'
}

export interface ConfirmEmailResponse {
    code: EmailConfirmationResultCode
}

export type EmailConfirmationResultCode = 'email-confirmed' | 'invalid-token';

export interface SignInResponse {
    code: 'signed-in' | 'sign-in-failed' | '2fa-required'
}

export interface SignIn2FaResponse {
    code: 'signed-in' | 'sign-in-failed' | 'invalid-verification-code'
}

export type PasswordResetResultCode = 'password-reset' | 'invalid-token';


export interface ResetPasswordResponse {
    code: PasswordResetResultCode;
}

export interface SignInRecoveryCodeResponse {
    code: 'signed-in' | 'sign-in-failed' | 'invalid-recovery-code'
}

@Injectable({
    providedIn: 'root'
})
export class AuthApi {
    constructor(
        private _http: HttpClient) {        
    }

    public async signUp(args: {
        email: string;
        password: string;
        invitationCode: string | null;
    }): Promise<SignUpResponse> {
        const call = this
            ._http
            .post<SignUpResponse>(
                `/api/auth/sign-up`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async resendConfirmationLink(args: {
        email: string;
    }): Promise<ResendConfirmationEmailResponse> {
        const call = this
            ._http
            .post<ResendConfirmationEmailResponse>(
                `/api/auth/resend-confirmation-link`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async confirmEmail(args: {
        userExternalId: string;
        code: string;
    }): Promise<ConfirmEmailResponse> {
        const call = this
            ._http
            .post<ConfirmEmailResponse>(
                `/api/auth/confirm-email`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async singIn(args: {
        email: string;
        password: string;
        rememberMe: boolean;
    }): Promise<SignInResponse> {
        const call = this
            ._http
            .post<SignInResponse>(
                `/api/auth/sign-in`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async singIn2Fa(args: {
        verificationCode: string,
        rememberMe: boolean;
        rememberDevice: boolean;
    }): Promise<SignIn2FaResponse> {
        const call = this
            ._http
            .post<SignIn2FaResponse>(
                `/api/auth/sign-in-2fa`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async singInRecoveryCode(args: {
        recoveryCode: string;
    }): Promise<SignInRecoveryCodeResponse> {
        const call = this
            ._http
            .post<SignInRecoveryCodeResponse>(
                `/api/auth/sign-in-recovery-code`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        return await firstValueFrom(call);
    }

    public async forgotPassword(args: {
        email: string;
    }): Promise<void> {
        const call = this
            ._http
            .post<SignInResponse>(
                `/api/auth/forgot-password`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });
        
        await firstValueFrom(call);
    }

    public async resetPassword(args: {
        userExternalId: string;
        code: string;
        newPassword: string;
    }): Promise<ResetPasswordResponse> {
        const call = this
            ._http
            .post<ResetPasswordResponse>(
                `/api/auth/reset-password`, args, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });
        
        return await firstValueFrom(call);
    }
    
    public async signOut(): Promise<void> {
        const call = this
            ._http
            .post(
                `/api/auth/signout`, {
                headers: new HttpHeaders({
                    'Content-Type':  'application/json'
                })
            });

        await firstValueFrom(call);
    }
}