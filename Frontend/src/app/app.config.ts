import { ApplicationConfig, ErrorHandler, importProvidersFrom, provideZonelessChangeDetection } from '@angular/core';
import { PreloadAllModules, Router, provideRouter, withEnabledBlockingInitialNavigation, withInMemoryScrolling, withPreloading, withRouterConfig } from '@angular/router';
import { HTTP_INTERCEPTORS, provideHttpClient, withFetch, withInterceptorsFromDi } from '@angular/common/http';
import { AuthInterceptor } from './services/auth.interceptor';
import { ToastrModule, ToastrService } from 'ngx-toastr';
import { AuthService } from './services/auth.service';
import { LoadingChunkFailedErrorHandler } from './services/loading-chunk-failed-error.handler';
import { Routes } from '@angular/router';
import { AdminGuardService } from './services/auth-guard.service';
import { AccessCodesApi } from './external-access/external-link/access-codes.api';
import { provideMarkdown } from 'ngx-markdown';

export const routes: Routes = [{
    path: '',
    redirectTo: 'sign-in',
    pathMatch: 'full'
}, {
    path: 'not-found',
    loadComponent: () => import('./static-pages/not-found-page/not-found-page.component').then(m => m.NotFoundPageComponent),
}, {
    path: 'sign-in',
    loadComponent: () => import('./static-pages/sign-in-page/sign-in-page.component').then(m => m.SignInPageComponent),
}, {
    path: 'sign-up',
    loadComponent: () => import('./static-pages/sign-up-page/sign-up-page.component').then(m => m.SignUpPageComponent),
}, {
    path: 'email-confirmation',
    loadComponent: () => import('./static-pages/email-confirmation-page/email-confirmation-page.component').then(m => m.EmailConfirmationPageComponent),
}, {
    path: 'reset-password',
    loadComponent: () => import('./static-pages/reset-password-page/reset-password-page.component').then(m => m.ResetPasswordPageComponent),
}, {
    path: 'terms',
    loadComponent: () => import('./static-pages/terms/terms.component').then(m => m.TermsPageComponent),
}, {
    path: 'privacy-policy',
    loadComponent: () => import('./static-pages/privacy-policy/privacy-policy.component').then(m => m.PrivacyPolicyPageComponent),
},  {
    path: 'workspaces',
    loadComponent: () => import('./workspaces/workspaces.component').then(m => m.WorkspacesComponent),
}, {
    path: 'account',
    loadComponent: () => import('./account/account.component').then(m => m.AccountComponent)
}, {
    path: 'account/mfa',
    loadComponent: () => import('./account/multi-factor-auth/multi-factor-auth.component').then(m => m.MultiFactorAuthComponent)
}, {
    path: 'settings/general',
    loadComponent: () => import('./account/general-settings/general-settings.component').then(m => m.GeneralSettingsComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/storage',
    loadComponent: () => import('./account/storage-settings/storage-settings.component').then(m => m.StorageSettingsComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/storage/add/hard-drive',
    loadComponent: () => import('./account/storage-settings/hard-drive/create-hard-drive-storage/create-hard-drive-storage.component').then(m => m.CreateHardDriveStorageComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/storage/add/digital-ocean-spaces',
    loadComponent: () => import('./account/storage-settings/digitalocean/create-digitalocean-storage/create-digitalocean-storage.component').then(m => m.CreateDigitalOceanStorageComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/storage/add/backblaze-b2',
    loadComponent: () => import('./account/storage-settings/backblaze/create-backblaze-storage/create-backblaze-storage.component').then(m => m.CreateBackblazeStorageComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/storage/add/cloudflare-r2',
    loadComponent: () => import('./account/storage-settings/cloudflare/create-cloudflare-storage/create-cloudflare-storage.component').then(m => m.CreateCloudflareStorageComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/storage/add/aws-s3',
    loadComponent: () => import('./account/storage-settings/aws/create-aws-storage/create-aws-storage.component').then(m => m.CreateAwsStorageComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/email',
    loadComponent: () => import('./account/email-settings/email-settings.component').then(m => m.EmailSettingsComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/users',
    loadComponent: () => import('./account/users-settings/users-settings.component').then(m => m.UsersSettingsComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/users/:userExternalId',
    loadComponent: () => import('./account/user-details/user-details.component').then(m => m.UserDetailsComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/integrations',
    loadComponent: () => import('./account/integrations/integrations.component').then(m => m.IntegrationsComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/integrations/add/aws-textract',
    loadComponent: () => import('./account/integrations/aws/textract/create/create-aws-textract.component').then(m => m.CreateAwsTextractComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'settings/integrations/add/openai-chatgpt',
    loadComponent: () => import('./account/integrations/openai/chatgpt/create/create-chatgpt.component').then(m => m.CreateChatGptComponent),
    canActivate: [AdminGuardService]
}, {
    path: 'workspaces/:workspaceExternalId',
    loadComponent: () => import('./workspace-manager/workspace-manager.component').then(m => m.WorkspaceManagerComponent),
    children: [{
        path: 'explorer/:folderExternalId',
        loadComponent: () => import('./workspace-manager/explorer/explorer.component').then(m => m.ExplorerComponent)
    }, {
        path: 'explorer',
        redirectTo: 'explorer/',
        pathMatch: 'full'
    }, {
        path: '',
        redirectTo: 'explorer/',
        pathMatch: 'full'
    }, {
        path: 'uploads',
        loadComponent: () => import('./workspace-manager/uploads/uploads.component').then(m => m.UploadsComponent)
    }, {
        path: 'boxes',
        loadComponent: () => import('./workspace-manager/boxes/boxes.component').then(m => m.BoxesComponent)
    }, {
        path: 'boxes/:boxExternalId/:tab',
        loadComponent: () => import('./workspace-manager/boxes/box-details/box-details.component').then(m => m.BoxDetailsComponent)        
    }, {
        path: 'boxes/:boxExternalId',
        redirectTo: 'boxes/:boxExternalId/layout',
        pathMatch: 'full'
    }, {
        path: 'team',
        loadComponent: () => import('./workspace-manager/team/team.component').then(m => m.TeamComponent)
    }, {
        path: 'config',
        loadComponent: () => import('./workspace-manager/config/workspace-config.component').then(m => m.WorkspaceConfigComponent)
    }]
}, {
    path: 'link/:accessCode/:folderExternalId',
    loadComponent: () => import('./external-access/external-link/external-link.component').then(m => m.ExternalLinkComponent)
}, {
    path: 'link/:accessCode',
    redirectTo: 'link/:accessCode/',
    pathMatch: 'full'
}, {
    path: 'box/:boxExternalId/:folderExternalId',
    loadComponent: () => import('./external-access/external-box/external-box.component').then(m => m.ExternalBoxComponent),
}, {
    path: 'box/:boxExternalId',
    redirectTo: 'box/:boxExternalId/',
    pathMatch: 'full'
}];

export const appConfig: ApplicationConfig = {
    providers: [
        provideZonelessChangeDetection(),
        provideRouter(
            routes,
            withEnabledBlockingInitialNavigation(),
            withInMemoryScrolling({
                scrollPositionRestoration: 'enabled'
            }),
            withPreloading(PreloadAllModules)),
        provideHttpClient(withInterceptorsFromDi(), withFetch()), {
            provide: HTTP_INTERCEPTORS,
            useClass: AuthInterceptor,
            multi: true,
            deps: [
                Router,
                AuthService,
                ToastrService,
                AccessCodesApi
            ]
        }, {
            provide: ErrorHandler,
            useClass: LoadingChunkFailedErrorHandler
        },
        importProvidersFrom([
            ToastrModule.forRoot(),
        ]),
        provideMarkdown()
    ]
};
