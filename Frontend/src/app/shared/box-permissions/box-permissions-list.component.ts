import { Component, WritableSignal, computed, input, output, signal } from "@angular/core";
import { toggle } from "../signal-utils";
import { BoxPermissions } from "../../services/boxes.api";
import { PermissionButtonComponent } from "../buttons/permission-btn/permission-btn.component";

export type AppBoxPermissions = {
    allowDownload: WritableSignal<boolean>;
    allowUpload: WritableSignal<boolean>;
    allowList: WritableSignal<boolean>;
    allowDeleteFile: WritableSignal<boolean>;
    allowDeleteFolder: WritableSignal<boolean>;
    allowRenameFile: WritableSignal<boolean>;
    allowRenameFolder: WritableSignal<boolean>;
    allowMoveItems: WritableSignal<boolean>;
    allowCreateFolder: WritableSignal<boolean>;
}

export function mapPermissionsToDto(permissions: AppBoxPermissions): BoxPermissions {
    return {
        allowUpload: permissions.allowUpload(),
        allowList: permissions.allowList(),
        allowDownload: permissions.allowList() && permissions.allowDownload(),
        allowDeleteFile: permissions.allowList() && permissions.allowDeleteFile(),
        allowDeleteFolder: permissions.allowList() && permissions.allowDeleteFolder(),
        allowRenameFile: permissions.allowList() && permissions.allowRenameFile(),
        allowRenameFolder: permissions.allowList() && permissions.allowRenameFolder(),
        allowMoveItems: permissions.allowList() && permissions.allowMoveItems(),
        allowCreateFolder: permissions.allowList() && permissions.allowCreateFolder()
    };
}

export function mapDtoToPermissions(dto: BoxPermissions): AppBoxPermissions {
    return {
        allowCreateFolder: signal(dto.allowCreateFolder),
        allowDeleteFile: signal(dto.allowDeleteFile),
        allowDeleteFolder: signal(dto.allowDeleteFolder),
        allowDownload: signal(dto.allowDownload),
        allowList: signal(dto.allowList),
        allowMoveItems: signal(dto.allowMoveItems),
        allowRenameFile: signal(dto.allowRenameFile),
        allowRenameFolder: signal(dto.allowRenameFolder),
        allowUpload: signal(dto.allowUpload)
    }
}

@Component({
    selector: 'app-box-permissions-list',
    imports: [
        PermissionButtonComponent
    ],
    styleUrl: './box-permissions-list.component.scss',
    templateUrl: './box-permissions-list.component.html'
})
export class BoxPermissionsListComponent {
    permissions = input.required<AppBoxPermissions>();  

    changed = output<void>();

    allowUpload = computed(() => this.permissions().allowUpload());
    allowList = computed(() => this.permissions().allowList());
    allowDownload = computed(() => this.permissions().allowDownload());
    allowDeleteFile = computed(() => this.permissions().allowDeleteFile());
    allowRenameFile = computed(() => this.permissions().allowRenameFile());
    allowCreateFolder = computed(() => this.permissions().allowCreateFolder());
    allowDeleteFolder = computed(() => this.permissions().allowDeleteFolder());
    allowRenameFolder = computed(() => this.permissions().allowRenameFolder());
    allowMoveItems = computed(() => this.permissions().allowMoveItems());

    
    private debounceTimers: any | null;
    async changePermissions(permissionName: 'allowUpload' | 'allowList' | 'allowDownload' | 'allowDeleteFile' | 'allowDeleteFolder' | 'allowRenameFile' | 'allowRenameFolder' | 'allowMoveItems' | 'allowCreateFolder') {        
        
        const permissions = this.permissions();
        
        switch (permissionName) {
            case 'allowDownload': toggle(permissions.allowDownload); break;
            case 'allowUpload': toggle(permissions.allowUpload); break;            
            case 'allowList': toggle(permissions.allowList); break;
            case 'allowDeleteFile': toggle(permissions.allowDeleteFile); break;
            case 'allowDeleteFolder': toggle(permissions.allowDeleteFolder); break;
            case 'allowRenameFile': toggle(permissions.allowRenameFile); break;
            case 'allowRenameFolder': toggle(permissions.allowRenameFolder); break;
            case 'allowMoveItems': toggle(permissions.allowMoveItems); break;
            case 'allowCreateFolder': toggle(permissions.allowCreateFolder); break;
            default:
                throw new Error('Unknown permission name ' + permissionName);
        }

        if(this.debounceTimers)
            clearTimeout(this.debounceTimers);           

        this.debounceTimers = setTimeout(async () =>  this.changed.emit(), 500);        
    }    
}