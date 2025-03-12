import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

@Injectable({
    providedIn: 'root'
})
export class AdminGuardService {
    constructor(
        public auth: AuthService,
        public router: Router) { }

    async canActivate(): Promise<boolean> {
        const isAuthenticated = await this.auth.isAuthenticatedAsync()

        if(!isAuthenticated) {
            this.router.navigate(['account']);
            return false;            
        }

        const isAdmin = this.auth.isAdmin();

        if (!isAdmin) {
            this.router.navigate(['account']);
            return false;
        }

        return true;
    }
}