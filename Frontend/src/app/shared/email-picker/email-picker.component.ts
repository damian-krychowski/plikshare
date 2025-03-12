import { Component, computed, OnDestroy, OnInit, Signal, signal, ViewEncapsulation, WritableSignal } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormControl, FormGroupDirective, FormsModule, NgForm } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { ActionButtonComponent } from '../buttons/action-btn/action-btn.component';
import { AccountApi, KnownUser } from '../../services/account.api';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { Subscription } from 'rxjs';

export class MyErrorStateMatcher implements ErrorStateMatcher {
    isErrorState(control: FormControl | null, form: FormGroupDirective | NgForm | null): boolean {
      const isSubmitted = form && form.submitted;
      return !!(control && control.invalid && (control.dirty || control.touched || isSubmitted));
    }
}

type EmailControl = {
    value: WritableSignal<string>;
    id: string;
    filteredKnownUsers: Signal<EmailUser[]>
}

type EmailUser = {
    externalId: string;
    email: string;
    highlightedEmail: string;
};

@Component({
    selector: 'app-email-picker',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        ActionButtonComponent,
        MatAutocompleteModule
    ],
    templateUrl: './email-picker.component.html',
    styleUrls: ['./email-picker.component.scss'],
    encapsulation: ViewEncapsulation.None
})
export class EmailPickerComponent implements OnInit, OnDestroy {
    emails = signal<EmailControl[]>([]);

    alreadyPickedEmails = computed(() => this.emails().filter(e => e.value()).map(e => e.value()))
    knownUsers = signal<EmailUser[]>([]);

    matcher = new MyErrorStateMatcher();

    private _subscription: Subscription | null = null;
    
    constructor(
        private _accountApi: AccountApi,
        public dialogRef: MatDialogRef<EmailPickerComponent>) {    
    }

    async ngOnInit(): Promise<void> {
        try {
            const result = await this
                ._accountApi
                .getKnownUsers();

            this.knownUsers.set(result.items.map(u => ({
                externalId: u.externalId,
                email: u.email,
                highlightedEmail: u.email
            })));

            this.addEmail();
        } catch (error) {
            console.error(error);
        }
    }

    ngOnDestroy(): void {
        this._subscription?.unsubscribe();
    }

    addEmail() {
        const emailValue = signal('');
        const id = crypto.randomUUID();

        this.emails.update(emails => [...emails, {
            value: emailValue, 
            id: id,
            filteredKnownUsers: computed(() => {
                const allUsers = this.knownUsers();
                const alreadyPickedEmails = this.alreadyPickedEmails();

                const value = emailValue();
                const outstandingUsers = allUsers.filter(user => !alreadyPickedEmails.includes(user.email));
    
                if(!value)
                    return outstandingUsers;
        
                const filter = value.toLowerCase();

                return outstandingUsers
                    .filter(user => user.email.includes(filter))
                    .map(user => ({
                        externalId: user.externalId,
                        email: user.email,
                        highlightedEmail: this.getUserEmailWithHighlight(user.email, filter)
                    }));
            })
        }]);
    }

    removeEmail(id: string) {        
        this.emails.update(emails => {
            const index = emails.findIndex(email => email.id === id);
            emails.splice(index, 1);   

            return emails;
        });           
    }

    public onEmailPicked() {
        this.dialogRef.close(this.emails().map(email => email.value()));
    }

    public onCancel() {
        this.dialogRef.close();
    }

    public getUserEmailWithHighlight(email: string, searchPhrase: string | null): string {
        const emailLowered = email.toLowerCase();

        const searchPhraseLowered = searchPhrase?.toLowerCase();
       
        if (!searchPhraseLowered || !emailLowered.includes(searchPhraseLowered)) {
          return email;
        }
    
        const startIndex = emailLowered.indexOf(searchPhraseLowered);
        const endIndex = startIndex + searchPhraseLowered.length;
        const highlightedEmail = `${email.slice(0, startIndex)}<strong>${email.slice(startIndex, endIndex)}</strong>${email.slice(endIndex)}`;
        
        return highlightedEmail;
    }
}
