import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

@Injectable({
    providedIn: 'root'
})
export class AuthenticatedGuardService {
    constructor(
        public auth: AuthService,
        public router: Router) { }

    async canActivate(): Promise<boolean> {
        const isAuthenticated = await this.auth.isAuthenticatedAsync();

        if (!isAuthenticated) {
            this.router.navigate(['sign-in']);
            return false;
        }

        return true;
    }
}
