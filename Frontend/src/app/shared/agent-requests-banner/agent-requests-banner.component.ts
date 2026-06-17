import { Component, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { AgentRequestsService } from '../../services/agent-requests.service';
import { MobileMenuStateService } from '../../services/mobile-menu-state.service';
import { OnScreenDirective } from '../on-screen.directive';

const INBOX_PATH = 'settings/agent-requests';

@Component({
    selector: 'app-agent-requests-banner',
    standalone: true,
    imports: [
        OnScreenDirective
    ],
    templateUrl: './agent-requests-banner.component.html',
    styleUrl: './agent-requests-banner.component.scss'
})
export class AgentRequestsBannerComponent {
    isTabVisible = signal(true);
    isOnInboxPage = signal(false);

    constructor(
        public agentRequests: AgentRequestsService,
        public mobileMenu: MobileMenuStateService,
        private _router: Router) {
        this.isOnInboxPage.set(this._router.url.includes(INBOX_PATH));

        this._router.events
            .pipe(takeUntilDestroyed())
            .subscribe(event => {
                if (event instanceof NavigationEnd)
                    this.isOnInboxPage.set(event.urlAfterRedirects.includes(INBOX_PATH));
            });
    }

    onTabVisibility(isVisible: boolean) {
        this.isTabVisible.set(isVisible);
    }

    open() {
        this._router.navigate([INBOX_PATH]);
    }
}
