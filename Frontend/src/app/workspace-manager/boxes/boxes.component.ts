import { Component, OnDestroy, OnInit, WritableSignal, computed, signal } from '@angular/core';
import { ActivatedRoute, Navigation, NavigationEnd, NavigationExtras, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { FolderPickerComponent } from './folder-picker/folder-picker.component';
import { BoxesSetApi } from '../../services/boxes.api';
import { InAppSharing } from '../../services/in-app-sharing.service';
import { AppBox, BoxItemComponent } from '../../shared/box-item/box-item.component';
import { DataStore } from '../../services/data-store.service';
import { Subscription, filter } from 'rxjs';
import { AppFolderItem } from '../../shared/folder-item/folder-item.component';
import { ActionButtonComponent } from '../../shared/buttons/action-btn/action-btn.component';
import { ItemButtonComponent } from '../../shared/buttons/item-btn/item-btn.component';

@Component({
    selector: 'app-boxes',
    imports: [
        ActionButtonComponent,
        ItemButtonComponent,
        BoxItemComponent
    ],
    templateUrl: './boxes.component.html',
    styleUrl: './boxes.component.scss'
})
export class BoxesComponent implements OnInit, OnDestroy {
    isLoadingBoxes = signal(false);
    isDeleting = signal(false);
    isLoading = computed(() => this.isLoadingBoxes() || this.isDeleting());
    isAnyBoxEditing = computed(() => this.boxes().some((b) => b.isNameEditing()));

    boxes: WritableSignal<AppBox[]> = signal([]);
    
    private _routerSubscription: Subscription | null = null;
    private currentWorkspaceExternalId: string | null = null;

    constructor(
        private _router: Router,
        private _boxesSetApi: BoxesSetApi,
        private _dialog: MatDialog,
        private _inAppSharing: InAppSharing,
        private _dataStore: DataStore,
        private _activatedRoute: ActivatedRoute
    ) {
    }

    async ngOnInit(){
        await this.handleNavigationChange(this._router.lastSuccessfulNavigation);
                
        this._routerSubscription = this._router.events
            .pipe(filter(event => event instanceof NavigationEnd))
            .subscribe(() => {
                const navigation = this._router.getCurrentNavigation();
                this.handleNavigationChange(navigation);            
            });
    }

    private async handleNavigationChange(navigation: Navigation | null) {
        await this.load();
        this.tryConsumeNavigationState(navigation);
    }

    private async load() {
        const workspaceExternalId = this._activatedRoute.parent?.snapshot.params['workspaceExternalId'];

        if(!workspaceExternalId)
            throw new Error('workspaceExternalId is missing');

        await this.loadBoxesIfNeeded(workspaceExternalId);                
    }

    ngOnDestroy(): void {
        this._routerSubscription?.unsubscribe();
    }

    private async tryConsumeNavigationState(navigation: Navigation | null) {
        if(!navigation || !navigation.extras)
            return;

        await this.tryCreateBoxFromFolder(navigation.extras);
        this.tryHighlightBox(navigation.extras);        
    }

    private async tryCreateBoxFromFolder(extras: NavigationExtras){
        if(!extras.state || !extras.state['folderToCreateBoxFrom'])
            return;
        
        const folderToCreateBoxFromKey = extras
            .state['folderToCreateBoxFrom'] as string;

        const folder = this
            ._inAppSharing
            .pop(folderToCreateBoxFromKey) as AppFolderItem;

        if(!folder)
            return;

        await this.createBoxFromFolder(folder);
    }

    private tryHighlightBox(extras: NavigationExtras) {
        if(!extras.state || !extras.state['boxToHighlight'])
            return;

        const boxToHighlightKey = extras
            .state['boxToHighlight'] as string;

        const boxExternalId = this
            ._inAppSharing
            .pop(boxToHighlightKey) as string;

        if(!boxExternalId)
            return;

        const box = this
            .boxes()
            .find((b) => b.externalId() === boxExternalId);

        if(box)
            box.isHighlighted.set(true);
    }

    private async loadBoxesIfNeeded(workspaceExternalId: string) {
        if(this.currentWorkspaceExternalId === workspaceExternalId)
            return;

        this.currentWorkspaceExternalId = workspaceExternalId;

        try {
            this.isLoadingBoxes.set(true);

            const response = await this
                ._dataStore
                .getBoxes(workspaceExternalId);

            this.boxes.set(response.items.map((box) => {
                const appBox: AppBox = {
                    externalId: signal(box.externalId),
                    workspaceExternalId: workspaceExternalId,
                    name: signal(box.name),
                    isEnabled: signal(box.isEnabled),
                    folderPath: signal(box.folderPath),            
                    isNameEditing: signal(false),
                    isHighlighted: signal(false)
                }

                return appBox;
            }));
        } catch (error) {
            console.error(error);
        } finally {
            this.isLoadingBoxes.set(false);
        }
    }


    buildFolderPathFromFolder(folder: AppFolderItem) {
        return folder
            .ancestors
            .map((p) => p.name)
            .concat([folder.name()])
            .join(' / ');
    }

    async createNewBox() {      
        const dialogRef = this._dialog.open(FolderPickerComponent, {
            width: '700px',
            data: {
                workspaceExternalId: this.currentWorkspaceExternalId,
            },
            maxHeight: '600px',
            position: {
                top: '100px'
            }   
        });

        dialogRef.afterClosed().subscribe(
            (folderToShare: AppFolderItem) => this.createBoxFromFolder(folderToShare));
    }

    async createBoxFromFolder(folder: AppFolderItem) {
        if(!folder)
            return;
            
        const name = signal('untitled box');
        const isEnabled = signal(true);

        const newBox: AppBox = {
            externalId: signal(null),
            name: name,
            isEnabled: isEnabled,
            workspaceExternalId: this.currentWorkspaceExternalId!,

            folderPath: signal([
                ...folder.ancestors, {
                    name: folder.name(),
                    externalId: folder.externalId
                }
            ]),

            isNameEditing: signal(true),
            isHighlighted: signal(false)
        }

        this.boxes.update(values => [...values, newBox]);

        const result = await this._boxesSetApi.createBox(this.currentWorkspaceExternalId!, {
            folderExternalId: folder.externalId,
            name: newBox.name()
        });

        newBox.externalId.set(result.externalId);
    }

    onBoxDelete(box: AppBox) {
        this.boxes.update(values => values.filter((f) => f.externalId() !== box.externalId()));
    }

}
