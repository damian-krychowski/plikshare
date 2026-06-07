import { Component, OnChanges, OnInit, SimpleChanges, input, output } from "@angular/core";
import { MatSelectModule } from "@angular/material/select";
import { FormsModule } from "@angular/forms";
import { ImageDimensionsPolicyDto } from "../../services/workspaces.api";

export type ImageDimensionsPolicyConfigChangedEvent = {
    imageDimensions: ImageDimensionsPolicyDto;
};

@Component({
    selector: 'app-image-dimensions-policy-config',
    standalone: true,
    imports: [
        MatSelectModule,
        FormsModule
    ],
    templateUrl: './image-dimensions-policy-config.component.html',
    styleUrl: './image-dimensions-policy-config.component.scss'
})
export class ImageDimensionsPolicyConfigComponent implements OnInit, OnChanges {
    imageDimensions = input.required<ImageDimensionsPolicyDto>();
    configChanged = output<ImageDimensionsPolicyConfigChangedEvent>();

    extractOptions: { value: boolean, label: string }[] = [
        { value: false, label: 'Disabled' },
        { value: true, label: 'Extract on upload' }
    ];
    extractOnUpload: boolean = false;

    ngOnInit() {
        this.initialize();
    }

    ngOnChanges(changes: SimpleChanges) {
        if (changes['imageDimensions']) {
            this.initialize();
        }
    }

    private initialize() {
        this.extractOnUpload = this.imageDimensions().extractOnUpload;
    }

    onModeChange() {
        this.configChanged.emit({
            imageDimensions: {
                extractOnUpload: this.extractOnUpload
            }
        });
    }
}
