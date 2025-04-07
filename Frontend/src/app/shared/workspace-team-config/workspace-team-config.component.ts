import { Component, OnInit, SimpleChanges, OnChanges, input, output } from "@angular/core";
import { MatButtonModule } from "@angular/material/button";
import { MatSelectModule } from "@angular/material/select";
import { FormsModule } from "@angular/forms";

export type WorkspaceMaxTeamMembersChangedEvent = {
    maxTeamMembers: number | null;
}

// Maximum allowed team members
const MAX_TEAM_MEMBERS = 2147483647;


@Component({
    selector: 'app-workspace-team-config',
    standalone: true,
    imports: [
        MatButtonModule,
        MatSelectModule,
        FormsModule
    ],
    templateUrl: './workspace-team-config.component.html',
    styleUrl: './workspace-team-config.component.scss'
})
export class WorkspaceTeamConfigComponent implements OnInit, OnChanges {
    maxTeamMembers = input.required<number | null>();
    configChanged = output<WorkspaceMaxTeamMembersChangedEvent>();
    
    limitOptions = ['Limited members', 'Unlimited members'];
    selectedLimit = 'Unlimited members';
    
    maxMembersValue: string = "0";
    hasError: boolean = false;
    errorMessage: string = '';

    formValid: boolean = true;

    ngOnInit() {
        this.initializeFromMaxTeamMembers();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['maxTeamMembers']) {
            this.initializeFromMaxTeamMembers();
        }
    }

    private initializeFromMaxTeamMembers() {
        const maxTeamMembers = this.maxTeamMembers();

        if (maxTeamMembers === null) {
            this.selectedLimit = 'Unlimited members';
            this.maxMembersValue = "0";
        } else {
            this.selectedLimit = 'Limited members';
            this.maxMembersValue = maxTeamMembers.toString();
        }
        this.validateMaxMembers();
    }

    onLimitChange() {
        this.validateMaxMembers();
        this.emitChanges();
    }

    onMembersValueChange(event: Event) {
        const value = (event.target as HTMLInputElement).value;
        this.maxMembersValue = value;
        this.validateMaxMembers();
        this.emitChanges();
    }

    validateMaxMembers(): boolean {
        this.hasError = false;
        this.errorMessage = '';
        
        if (this.selectedLimit === 'Unlimited members') {
            this.formValid = true;
            return true;
        }
        
        if (this.maxMembersValue == null || this.maxMembersValue == "") {
            this.hasError = true;
            this.errorMessage = 'Maximum members is required';
            this.formValid = false;
            return false;
        }
        
        if (parseInt(this.maxMembersValue) < 0) {
            this.hasError = true;
            this.errorMessage = 'Maximum members must be at least 0';
            this.formValid = false;
            return false;
        }

        if (parseInt(this.maxMembersValue) > MAX_TEAM_MEMBERS) {
            this.hasError = true;
            this.errorMessage = `Maximum members cannot exceed ${MAX_TEAM_MEMBERS}`;
            this.formValid = false;
            return false;
        }

        this.formValid = true;
        return true;
    }

    private emitChanges() {
        if (!this.formValid) {
            return;
        }
        
        const maxTeamMembers = this.selectedLimit === 'Limited members'
            ? parseInt(this.maxMembersValue)
            : null;
            
        this.configChanged.emit({ maxTeamMembers });
    }
}