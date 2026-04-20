import { HttpInterceptor, HttpErrorResponse, HttpRequest, HttpHandler, HttpEvent } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { Router } from "@angular/router";
import { Observable, of, throwError, catchError, from, switchMap, map, tap } from "rxjs";
import { ToastrService } from 'ngx-toastr';
import { AccessCodesApi } from "../external-access/external-link/access-codes.api";
import { SignOutService } from "./sign-out.service";
import { BOX_LINK_TOKEN_HEADER } from "./box-link-token.service";
import { UnlockFullEncryptionComponent } from "../shared/unlock-full-encryption/unlock-full-encryption.component";
import { SetupEncryptionPasswordComponent } from "../shared/setup-encryption-password/setup-encryption-password.component";
import { GenericDialogService } from "../shared/generic-message-dialog/generic-dialog-service";

const USER_ENCRYPTION_SESSION_REQUIRED = "user-encryption-session-required";
const USER_ENCRYPTION_SETUP_REQUIRED = "user-encryption-setup-required";
const CREATOR_ENCRYPTION_NOT_SET_UP = "creator-encryption-not-set-up";
const WORKSPACE_ENCRYPTION_PENDING_KEY_GRANT = "workspace-encryption-pending-key-grant";

@Injectable({
    providedIn: 'root'
})
export class AuthInterceptor implements HttpInterceptor {
    constructor(
        private _router: Router,
        private _signOutService: SignOutService,
        private _toastr: ToastrService,
        private _accessCodesApi: AccessCodesApi,
        private _dialog: MatDialog,
        private _genericDialog: GenericDialogService
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
            const body = this.parseErrorBody(err);

            if(err.error && err.error.code == "terms-not-accepted") {
                this._router.navigateByUrl(`/accept-terms`);
                return of(err.message);
            } else if (body?.code === WORKSPACE_ENCRYPTION_PENDING_KEY_GRANT) {
                // Proper modal explaining the situation + navigate back to the dashboard
                // so the user doesn't stare at an empty workspace view. The dashboard
                // marks pending workspaces with an icon so they know which one is blocked.
                return this._genericDialog
                    .openPendingKeyGrantDialog()
                    .pipe(
                        tap(() => this._router.navigateByUrl('/workspaces')),
                        switchMap(() => throwError(() => err)));
            } else {
                this._router.navigateByUrl(`#`);
                this._toastr.error("Something went wrong. Try again later or contact the administrator");
                return of(err.message);
            }
        } else if (err.status === 423) {
            const body = this.parseErrorBody(err);

            if (body?.code === USER_ENCRYPTION_SESSION_REQUIRED) {
                return this
                    .openUnlockDialog()
                    .pipe(switchMap(unlocked => unlocked
                        ? next.handle(req)
                        : throwError(() => err)));
            }

            if (body?.code === USER_ENCRYPTION_SETUP_REQUIRED) {
                return this
                    .openSetupDialog()
                    .pipe(switchMap(setUp => setUp
                        ? next.handle(req)
                        : throwError(() => err)));
            }
        } else if (err.status === 400) {
            const body = this.parseErrorBody(err);

            if (body?.code === CREATOR_ENCRYPTION_NOT_SET_UP) {
                this._toastr.error(
                    "You need to set up your encryption password first. Go to Settings > Your account to configure it.");
            }

            // let 400 propagate to caller
        } else if(err.status === 404) {
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

    private openUnlockDialog(): Observable<boolean> {
        const dialogRef = this._dialog.open<
            UnlockFullEncryptionComponent,
            void,
            boolean>(UnlockFullEncryptionComponent, {
                width: '500px',
                position: { top: '100px' },
                disableClose: true
            });

        return dialogRef.afterClosed().pipe(map(result => result === true));
    }

    private openSetupDialog(): Observable<boolean> {
        const dialogRef = this._dialog.open<
            SetupEncryptionPasswordComponent,
            void,
            boolean>(SetupEncryptionPasswordComponent, {
                width: '500px',
                position: { top: '100px' },
                disableClose: true
            });

        return dialogRef.afterClosed().pipe(map(result => result === true));
    }
}
