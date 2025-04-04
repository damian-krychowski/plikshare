import { HttpInterceptor, HttpErrorResponse, HttpRequest, HttpHandler, HttpEvent } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { Router } from "@angular/router";
import { Observable, of, throwError, catchError, from, switchMap } from "rxjs";
import { ToastrService } from 'ngx-toastr';
import { AccessCodesApi } from "../external-access/external-link/access-codes.api";
import { SignOutService } from "./sign-out.service";
import { AntiforgeryApi } from "./antiforgery.api";

@Injectable({
    providedIn: 'root'
})
export class AuthInterceptor implements HttpInterceptor {
    constructor(
        private _router: Router, 
        private _signOutService: SignOutService,
        private _toastr: ToastrService,
        private _accessCodesApi: AccessCodesApi,
        private _antiforgeryApi: AntiforgeryApi
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
            .pipe(catchError(x => this.handleError(x)));
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
                switchMap(() => {
                    return from(this._antiforgeryApi.fetchForBoxLink()).pipe(
                        switchMap(() => {
                            return next.handle(req);
                        })
                    );
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

    private handleError(err: HttpErrorResponse): Observable<any> {
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
        } else if(err.status === 404 || err.status === 400) {
            //ignore
        }
        else {
            console.log(err);
            this._toastr.error("Something went wrong. Try again later or contact the administrator");
        }

        return throwError(() => err);
    }
}
