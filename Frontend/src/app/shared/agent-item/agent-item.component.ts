import { Component, input, output, signal, WritableSignal } from "@angular/core";
import { ConfirmOperationDirective } from "../operation-confirm/confirm-operation.directive";
import { ActionButtonComponent } from "../buttons/action-btn/action-btn.component";
import { Operations, OptimisticOperation } from "../../services/optimistic-operation";
import { observeIsHighlighted } from "../../services/is-highlighted-utils";
import { RelativeTimeComponent } from "../relative-time/relative-time.component";
import { AgentsApi } from "../../services/agents.api";

export type AppAgent = {
    externalId: string;
    name: WritableSignal<string>;
    isEnabled: WritableSignal<boolean>;
    createdAt: string;

    isHighlighted: WritableSignal<boolean>;
}

@Component({
    selector: 'app-agent-item',
    imports: [
        ConfirmOperationDirective,
        ActionButtonComponent,
        RelativeTimeComponent
    ],
    templateUrl: './agent-item.component.html',
    styleUrl: './agent-item.component.scss'
})
export class AgentItemComponent {
    agent = input.required<AppAgent>();

    deleted = output<OptimisticOperation>();
    clicked = output<void>();

    isHighlighted = observeIsHighlighted(this.agent);

    areActionsVisible = signal(false);

    constructor(
        private _agentsApi: AgentsApi
    ) { }

    onClicked() {
        this.clicked.emit();
    }

    toggleActions() {
        this.areActionsVisible.update(value => !value);
    }

    async delete() {
        const agent = this.agent();
        const operation = Operations.optimistic();

        this.deleted.emit(operation);

        try {
            await this._agentsApi.deleteAgent(
                agent.externalId);

            operation.succeeded();
        } catch (error) {
            console.error(error);
            operation.failed(error);
        }
    }
}
