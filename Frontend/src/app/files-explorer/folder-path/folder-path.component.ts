import { AfterViewInit, Component, computed, ElementRef, Host, input, OnDestroy, output, signal, Signal, ViewChild, WritableSignal } from "@angular/core";
import { TextMeasurementService } from "../../services/text-measurement.service";
import { PrefetchDirective } from "../../shared/prefetch.directive";
import { DragOverStayDirective } from "../directives/drag-over-stay.directive";

export type AppFolderPath = {
    externalId: string;
    name: Signal<string>;
    ancestors: AppFolderPathAncestor[];
}

export type AppFolderPathAncestor = {
    externalId: string;
    name: string;
}

type PathSegment = {
    externalId: string | null;
    name: string;
    width: number;
}

const PADDING_WIDTH = 10;
const ICON_WIDTH = 20;
const DOTS_WIDTH = 20;
const DOTS_SEGMENT_WIDTH = DOTS_WIDTH + ICON_WIDTH;

@Component({
    selector: 'app-folder-path',
    templateUrl: './folder-path.component.html',
    styleUrl: './folder-path.component.scss',
    imports: [
        PrefetchDirective,
        DragOverStayDirective
    ]
})
export class FolderPathComponent implements AfterViewInit, OnDestroy {    
    topFolderExternalId = input<string>();
    selectedFolder = input.required<AppFolderPath | null>();

    prefetchFolder = output<string | null>();
    openFolder = output<string | null>();
    currentFolderClick = output();

    visibleAncestors = signal<AppFolderPathAncestor[]>([]);
    shouldShowEllipsis = signal(false);

    selectedFolderExternalId = computed(() => this.selectedFolder()?.externalId ?? null);
    maxPathWidth = signal(0);

    allPathSegments = computed(() => {
        const selectedFolder = this.selectedFolder();

        const segments: PathSegment[] = [];

        if(this.topFolderExternalId() == null) {
            const name = "All files";

            segments.push({
                externalId: null,
                name: name,
                width: this.measureSegmentWidth(name, !!selectedFolder)
            });
        }

        if(!selectedFolder)
            return segments;

        const ancestors = selectedFolder.ancestors ?? [];

        for (let index = 0; index < ancestors.length; index++) {
            const ancestor = ancestors[index];

            segments.push({
                externalId: ancestor.externalId,
                name: ancestor.name,
                width: this.measureSegmentWidth(ancestor.name, true)
            });
        }

        segments.push({
            externalId: selectedFolder.externalId,
            name: selectedFolder.name(),
            width: this.measureSegmentWidth(selectedFolder.name(), false)
        });

        return segments;
    });

    allPathSegmentsLength = computed(() => this.allPathSegments().length)

    visiblePathSegments = computed(() => {
        const maxWidth = this.maxPathWidth() - DOTS_SEGMENT_WIDTH;
        
        const allSegments = this.allPathSegments();
        
        if(!allSegments || !allSegments.length)
            return [];

        const lastSegment = allSegments[allSegments.length - 1];

        const segments: PathSegment[] = [lastSegment];
        let segmentsWidth = lastSegment.width;

        for (let index = allSegments.length - 2; index >= 0; index--) {
            const segment = allSegments[index];
            
            segmentsWidth += segment.width;

            if(segmentsWidth >= maxWidth)
                return segments;

            segments.unshift(segment);
        }

        return segments;
    });

    lastHiddenSegment = computed(() => {
        const allPathSegments = this.allPathSegments();
        const visiblePathSegments = this.visiblePathSegments();

        if(allPathSegments.length == visiblePathSegments.length) 
            return null;

        const lastHiddenSegmentIndex = allPathSegments.length - visiblePathSegments.length - 1;

        return allPathSegments[lastHiddenSegmentIndex];
    });

    private resizeObserver: ResizeObserver;

    constructor(
        @Host() private host: ElementRef,
        private _textMeasurement: TextMeasurementService
    ) {
        this.resizeObserver = new ResizeObserver(() => {
            this.maxPathWidth.set(this.host.nativeElement.offsetWidth)
        });
    }

    ngAfterViewInit() {
        this.resizeObserver.observe(this.host.nativeElement);
    }

    ngOnDestroy() {
        this.resizeObserver.disconnect();
    }

    private measureSegmentWidth(name: string, includeIcon: boolean): number {
        const textWidth = this._textMeasurement.measureSegmentWidth(name, {
            fontFamily: "Inter, sans-serif",
            fontSize: "1rem"
        });

        if(includeIcon) {
            return textWidth + PADDING_WIDTH + ICON_WIDTH;
        }

        return textWidth;
    }

    onFolderPrefetch(externalId: string | null) {
        this.prefetchFolder.emit(externalId);
    }

    onFolderClick(externalId: string | null) {
        if(this.selectedFolderExternalId() === externalId) {
            this.currentFolderClick.emit();
        } else {
            this.openFolder.emit(externalId);
        }
    }

    onFolderDragOver(externalId: string | null) {
        if(this.selectedFolderExternalId() === externalId) {
            return;
        } 

        this.openFolder.emit(externalId);
    }
}