import { HttpInterceptor, HttpErrorResponse, HttpRequest, HttpHandler, HttpEvent } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { Router } from "@angular/router";
import { Observable, of, throwError, catchError, from, switchMap, map } from "rxjs";
import { ToastrService } from 'ngx-toastr';
import { AccessCodesApi } from "../external-access/external-link/access-codes.api";
import { SignOutService } from "./sign-out.service";
import { BOX_LINK_TOKEN_HEADER } from "./box-link-token.service";
import { UnlockFullEncryptionComponent, UnlockFullEncryptionDialogData } from "../shared/unlock-full-encryption/unlock-full-encryption.component";

const FULL_ENCRYPTION_SESSION_REQUIRED = "full-encryption-session-required";

@Injectable({
    providedIn: 'root'
})
export class AuthInterceptor implements HttpInterceptor {
    constructor(
        private _router: Router,
        private _signOutService: SignOutService,
        private _toastr: ToastrService,
        private _accessCodesApi: AccessCodesApi,
        private _dialog: MatDialog
        ){}

    intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        //don't intercept requests to cloudflare
        if(req.url.indexOf("cloudflare") > -1) {
            return next.handle(req);
        }

        if(req.url.indexOf("api/access-codes") > -1) {
            return this.handleBoxLinkInterceptionPath(req, next);
        }

        return  next
            .handle(req)
            .pipe(catchError(x => this.handleError(x, req, next)));
    }

    private handleBoxLinkInterceptionPath(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
        return next
            .handle(req)
            .pipe(catchError(x => this.handleBoxLinkError(x, req, next)));
    }

    private handleBoxLinkError(err: HttpErrorResponse, req: HttpRequest<any>, next: HttpHandler): Observable<any> {
        //handle your auth error or rethrow
        if (err.status === 401) {
            return from(this._accessCodesApi.startSession()).pipe(
                switchMap((token) => {
                    const authReq = req.clone({
                        headers: req.headers.set(BOX_LINK_TOKEN_HEADER, token)
                    });

                    return next.handle(authReq);
                })
            );
        } else if(err.status === 404) {
            this._router.navigateByUrl(`/not-found`);
        }
        else {
            this._toastr.error("Something went wrong. Try again later or contact the administrator");
        }

        return throwError(() => err);
    }

    private handleError(err: HttpErrorResponse, req: HttpRequest<any>, next: HttpHandler): Observable<any> {
        if (err.status === 401) {
            this._signOutService.signOutAndNavigateByUrl(`/sign-in`);

            return of(err.message);
        } else if (err.status === 403) {
            if(err.error && err.error.code == "terms-not-accepted") {
                this._router.navigateByUrl(`/accept-terms`);
                return of(err.message);
            } else {
                this._router.navigateByUrl(`#`);
                this._toastr.error("Something went wrong. Try again later or contact the administrator");
                return of(err.message);
            }
        } else if (err.status === 423) {
            const body = this.parseErrorBody(err);

            if (body?.code === FULL_ENCRYPTION_SESSION_REQUIRED && body?.storageExternalId) {
                return this
                    .openUnlockDialog(body.storageExternalId)
                    .pipe(switchMap(unlocked => unlocked
                        ? next.handle(req)
                        : throwError(() => err)));
            }
        } else if(err.status === 404 || err.status === 400) {
            //ignore
        }
        else {
            console.error(err);
            this._toastr.error("Something went wrong. Try again later or contact the administrator");
        }

        return throwError(() => err);
    }

    private parseErrorBody(err: HttpErrorResponse): any {
        const body = err.error;

        if (body instanceof ArrayBuffer) {
            try {
                const text = new TextDecoder().decode(body);
                return JSON.parse(text);
            } catch {
                return null;
            }
        }

        if (typeof body === 'string') {
            try {
                return JSON.parse(body);
            } catch {
                return null;
            }
        }

        return body;
    }

    private openUnlockDialog(storageExternalId: string): Observable<boolean> {
        const dialogRef = this._dialog.open<
            UnlockFullEncryptionComponent,
            UnlockFullEncryptionDialogData,
            boolean>(UnlockFullEncryptionComponent, {
                width: '500px',
                position: { top: '100px' },
                disableClose: true,
                data: { storageExternalId }
            });

        return dialogRef.afterClosed().pipe(map(result => result === true));
    }
}
