import { computed, effect, Injectable, OnDestroy, signal } from '@angular/core';
import { Subject, Subscription, takeUntil, timer } from 'rxjs';
import { AgentsApi, PendingAgentOperation } from './agents.api';
import { AuthService } from './auth.service';

const POLL_INTERVAL_MS = 5000;

@Injectable({
    providedIn: 'root'
})
export class AgentRequestsService implements OnDestroy {
    private _destroy$ = new Subject<void>();
    private _polling: Subscription;
    private _inFlight = false;

    public pending = signal<PendingAgentOperation[]>([]);
    public count = computed(() => this.pending().length);

    constructor(
        private _agentsApi: AgentsApi,
        private _auth: AuthService) {
        effect(() => {
            if (this._auth.canManageAgents())
                this.poll();
        });

        this._polling = timer(0, POLL_INTERVAL_MS)
            .pipe(takeUntil(this._destroy$))
            .subscribe(() => this.poll());
    }

    public async refresh(): Promise<void> {
        await this.poll();
    }

    public remove(operationExternalId: string): void {
        this.pending.update(items =>
            items.filter(item => item.externalId !== operationExternalId));
    }

    private async poll(): Promise<void> {
        if (!this._auth.canManageAgents()) {
            if (this.pending().length > 0)
                this.pending.set([]);

            return;
        }

        if (this._inFlight)
            return;

        this._inFlight = true;

        try {
            const response = await this._agentsApi.getPendingOperations();
            this.pending.set(response.items);
        } catch (error) {
            console.error('Failed to fetch pending agent operations:', error);
        } finally {
            this._inFlight = false;
        }
    }

    ngOnDestroy(): void {
        this._destroy$.next();
        this._destroy$.complete();
        this._polling?.unsubscribe();
    }
}
