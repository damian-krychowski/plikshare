import { Component, DestroyRef, ElementRef, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { EMPTY_MINIMAP_ITEM_STATE, MinimapBlock, MinimapItemRef, MinimapItemState, MinimapSegment } from './minimap-model';

const DRAW_OVERSCAN_PX = 24;
const DRAG_THRESHOLD_PX = 3;
const DOUBLE_CLICK_MS = 250;
const DOUBLE_CLICK_MOVE_PX = 6;
const MIN_INDICATOR_HEIGHT_PX = 8;
const SECTION_BAND_PX = 13;
const SECTION_BAND_GAP_PX = 4;
const MAP_TOP_GAP_PX = 6;
const MONTH_BAND_PX = 12;
const THUMB_MIN_BLOCK_PX = 4;
const THUMB_BITMAP_SIZE_PX = 64;
const THUMB_CACHE_MAX = 1500;
const THUMB_CONCURRENCY = 4;
const THUMB_LOAD_TIMEOUT_MS = 15000;
const ROUND_CLIP_MIN_PX = 10;
const BACK_BUFFER_SCREENS = 5;
const BACK_BUFFER_LEAD_SCREENS = 2;
const BACK_BUFFER_REFILL_SCREENS = 0.75;
const WHEEL_LINE_HEIGHT_PX = 16;
const HOST_BOTTOM_GAP_PX = 16;
const SCRIM_FADE_MS = 150;

const SECTION_FONT = '600 7.5px Inter, sans-serif';
const MONTH_FONT = '500 8px Inter, sans-serif';

const COLORS = {
    rowFill: '#dfe3e6',
    folderFill: '#ccd3d8',
    folderChip: '#aeb9c0',
    tileFill: '#dde1e4',
    selectedFill: '#5d676e',
    selectedStroke: '#434c52',
    selectedMat: '#ecf2f7',
    selectedMatStroke: '#b0bec5',
    hoverStroke: '#828c93',
    divider: '#eeeff1',
    monthText: '#61676b',
    sectionText: '#444',
    chevron: '#aab2b8',
    rowText: '#61676b',
    selectedRowFill: 'rgba(67, 76, 82, 0.14)',
    fileGlyph: '#b6bfc6',
    folderGlyph: '#9fabb3',
    checkboxBorder: '#b0bcc3',
    checkboxCheck: '#ffffff',
    scrimFill: 'rgba(236, 242, 247, 0.62)'
};

type SegmentGeometry = {
    key: string;
    sectionY: number | null;
    sectionHeight: number;
    contentY: number | null;
    contentWidth: number;
    contentHeight: number;
};

type MinimapGeometry = {
    contentWidth: number;
    contentHeight: number;
    segments: SegmentGeometry[];
};

type MinimapViewport = {
    top: number;
    height: number;
};

type AbsoluteBlock = {
    block: MinimapBlock;
    y: number;
    h: number;
    xf: number;
    wf: number;
    segmentWidth: number;
};


type AbsoluteBlocks = {
    blocks: AbsoluteBlock[];
    maxBlockHeight: number;
};

type YBand = {
    contentY: number;
    slotContent: number;
    extraPx: number;
    topGapPx: number;
    kind: 'section' | 'month';
    label: string;
    isExpanded: boolean;
};

type DragState = {
    pointerId: number;
    startY: number;
    grabOffset: number;
    moved: boolean;
    expectedTop: number;
};

type Tooltip = {
    label: string;
    top: number;
};

type ThumbWant = {
    blocks: AbsoluteBlock[];
    centerY: number;
};

function clamp(value: number, min: number, max: number): number {
    return Math.min(max, Math.max(min, value));
}

function numbersClose(a: number | null, b: number | null): boolean {
    if (a === null || b === null)
        return a === b;

    return Math.abs(a - b) < 0.5;
}

function geometriesEqual(a: MinimapGeometry, b: MinimapGeometry): boolean {
    if (!numbersClose(a.contentWidth, b.contentWidth))
        return false;

    if (!numbersClose(a.contentHeight, b.contentHeight))
        return false;

    if (a.segments.length !== b.segments.length)
        return false;

    for (let i = 0; i < a.segments.length; i++) {
        const sa = a.segments[i];
        const sb = b.segments[i];

        if (sa.key !== sb.key
            || !numbersClose(sa.sectionY, sb.sectionY)
            || !numbersClose(sa.sectionHeight, sb.sectionHeight)
            || !numbersClose(sa.contentY, sb.contentY)
            || !numbersClose(sa.contentWidth, sb.contentWidth)
            || !numbersClose(sa.contentHeight, sb.contentHeight))
            return false;
    }

    return true;
}

function sizesEqual(a: { w: number, h: number }, b: { w: number, h: number }): boolean {
    return a.w === b.w && a.h === b.h;
}

function tooltipsEqual(a: Tooltip | null, b: Tooltip | null): boolean {
    if (a === null || b === null)
        return a === b;

    return a.label === b.label && a.top === b.top;
}

function elementsEqual(a: Element[], b: Element[]): boolean {
    if (a.length !== b.length)
        return false;

    for (let i = 0; i < a.length; i++) {
        if (a[i] !== b[i])
            return false;
    }

    return true;
}

function lowerBound(blocks: AbsoluteBlock[], yMin: number): number {
    let lo = 0;
    let hi = blocks.length;

    while (lo < hi) {
        const mid = (lo + hi) >> 1;

        if (blocks[mid].y < yMin)
            lo = mid + 1;
        else
            hi = mid;
    }

    return lo;
}

function abbreviateMonthLabel(label: string): string {
    const match = label.match(/^([A-Za-z]+) (\d{4})$/);

    if (!match)
        return label;

    return `${match[1].slice(0, 3)} ${match[2]}`;
}

function roundRectPath(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    w: number,
    h: number,
    radius: number
): void {
    ctx.beginPath();

    if (typeof ctx.roundRect === 'function') {
        ctx.roundRect(x, y, w, h, radius);
    } else {
        ctx.rect(x, y, w, h);
    }
}

function decodeThumb(blob: Blob): Promise<ImageBitmap> {
    return createImageBitmap(
        blob,
        { resizeWidth: THUMB_BITMAP_SIZE_PX, resizeQuality: 'medium' })
        .catch(() => createImageBitmap(blob));
}

@Component({
    selector: 'app-files-minimap',
    standalone: true,
    templateUrl: './files-minimap.component.html',
    styleUrl: './files-minimap.component.scss',
    host: {
        '[class.files-minimap--pressed]': 'isPressed()',
        '[class.files-minimap--sunken]': 'isRailHovered() && !isPressed()',
        '[class.files-minimap--static]': '!isScrollable()',
        '[class.files-minimap--lift-hovered]': 'isLiftHovered()',
    }
})
export class FilesMinimapComponent {
    segments = input.required<MinimapSegment[]>();
    contentHost = input.required<HTMLElement>();
    stickyHeader = input<HTMLElement | null>(null);
    externalHoveredId = input<string | null>(null);

    itemCtrlClicked = output<MinimapItemRef>();
    itemShiftClicked = output<MinimapItemRef>();
    itemHovered = output<MinimapItemRef | null>();

    private _railRef = viewChild<ElementRef<HTMLElement>>('rail');
    private _canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');
    private _liftAnchorRef = viewChild<ElementRef<HTMLElement>>('liftAnchor');
    private _liftCanvasRef = viewChild<ElementRef<HTMLCanvasElement>>('liftCanvas');

    private _canvasSize = signal(
        { w: 0, h: 0 },
        { equal: sizesEqual });

    private _geometry = signal<MinimapGeometry>(
        { contentWidth: 0, contentHeight: 0, segments: [] },
        { equal: geometriesEqual });

    private _headerHeight = signal(0);
    private _isScrollableState = signal(false);

    tooltip = signal<Tooltip | null>(
        null,
        { equal: tooltipsEqual });

    isDragging = signal(false);
    isPressed = signal(false);
    isRailHovered = signal(false);
    isLiftHovered = signal(false);

    isScrollable = computed(() => this._isScrollableState());

    private _scale = computed(() => {
        const canvasWidth = this._canvasSize().w;
        const contentWidth = this._geometry().contentWidth;

        return canvasWidth > 0 && contentWidth > 0
            ? canvasWidth / contentWidth
            : 0;
    });

    private _absBlocks = computed<AbsoluteBlocks>(() => {
        const segments = this.segments();
        const geometry = this._geometry();

        const placed: { contentY: number, segment: MinimapSegment, segmentGeometry: SegmentGeometry }[] = [];

        for (const segment of segments) {
            const segmentGeometry = geometry.segments.find(g => g.key === segment.key);

            if (!segmentGeometry || segmentGeometry.contentY === null)
                continue;

            placed.push({
                contentY: segmentGeometry.contentY,
                segment,
                segmentGeometry
            });
        }

        placed.sort((a, b) => a.contentY - b.contentY);

        const blocks: AbsoluteBlock[] = [];
        let maxBlockHeight = 0;

        for (const { contentY, segment, segmentGeometry } of placed) {
            const modelHeight = segment.model.modelHeight;

            const yScale = modelHeight > 0 && segmentGeometry.contentHeight > 0
                ? segmentGeometry.contentHeight / modelHeight
                : 1;

            for (const block of segment.model.blocks) {
                const h = block.h * yScale;

                blocks.push({
                    block,
                    y: contentY + block.y * yScale,
                    h,
                    xf: block.xf,
                    wf: block.wf,
                    segmentWidth: segmentGeometry.contentWidth
                });

                if (h > maxBlockHeight)
                    maxBlockHeight = h;
            }
        }

        return { blocks, maxBlockHeight };
    });

    private _yBands = computed<YBand[]>(() => {
        const scale = this._scale();

        if (scale <= 0)
            return [];

        const geometry = this._geometry();
        const bands: YBand[] = [];

        for (const segment of this.segments()) {
            if (!segment.label)
                continue;

            const segmentGeometry = geometry.segments.find(g => g.key === segment.key);

            if (!segmentGeometry || segmentGeometry.sectionY === null)
                continue;

            const slotContent = segmentGeometry.contentY !== null
                ? Math.max(0, segmentGeometry.contentY - segmentGeometry.sectionY)
                : Math.max(0, segmentGeometry.sectionHeight);

            bands.push({
                contentY: segmentGeometry.sectionY,
                slotContent,
                extraPx: Math.max(0, SECTION_BAND_PX + SECTION_BAND_GAP_PX - slotContent * scale),
                topGapPx: 0,
                kind: 'section',
                label: segment.label,
                isExpanded: segmentGeometry.contentY !== null
            });
        }

        for (const absoluteBlock of this._absBlocks().blocks) {
            if (absoluteBlock.block.kind !== 'header')
                continue;

            bands.push({
                contentY: absoluteBlock.y,
                slotContent: absoluteBlock.h,
                extraPx: Math.max(0, MONTH_BAND_PX - absoluteBlock.h * scale),
                topGapPx: 0,
                kind: 'month',
                label: absoluteBlock.block.label ?? '',
                isExpanded: true
            });
        }

        bands.sort((a, b) => a.contentY - b.contentY);

        if (bands.length > 0 && bands[0].kind === 'section') {
            bands[0].topGapPx = MAP_TOP_GAP_PX;
            bands[0].extraPx += MAP_TOP_GAP_PX;
        }

        return bands;
    });

    private yToMini(contentY: number): number {
        const scale = this._scale();
        let extra = 0;

        for (const band of this._yBands()) {
            if (band.contentY < contentY)
                extra += band.extraPx;
            else
                break;
        }

        return contentY * scale + extra;
    }

    private miniToY(miniY: number): number {
        const scale = this._scale();

        if (scale <= 0)
            return 0;

        let extra = 0;

        for (const band of this._yBands()) {
            const bandStartMini = band.contentY * scale + extra;

            if (miniY <= bandStartMini)
                break;

            const bandHeight = band.slotContent * scale + band.extraPx;
            const bandEndMini = bandStartMini + bandHeight;

            if (miniY < bandEndMini) {
                const fraction = (miniY - bandStartMini) / Math.max(1e-6, bandHeight);

                return band.contentY + fraction * band.slotContent;
            }

            extra += band.extraPx;
        }

        return (miniY - extra) / scale;
    }

    private _itemState = computed<MinimapItemState>(() => {
        const selectedIds = new Set<string>();
        const cutIds = new Set<string>();

        for (const segment of this.segments()) {
            const state = segment.itemState?.() ?? EMPTY_MINIMAP_ITEM_STATE;

            for (const id of state.selectedIds)
                selectedIds.add(id);

            for (const id of state.cutIds)
                cutIds.add(id);
        }

        return { selectedIds, cutIds };
    });

    private _observedElements = computed<Element[]>(
        () => {
            const elements: Element[] = [this.contentHost()];
            const header = this.stickyHeader();

            if (header)
                elements.push(header);

            for (const segment of this.segments()) {
                if (segment.sectionEl)
                    elements.push(segment.sectionEl);

                if (segment.contentEl)
                    elements.push(segment.contentEl);
            }

            return elements;
        },
        { equal: elementsEqual });

    private readonly _hostEl: HTMLElement;
    private readonly _backCanvas = document.createElement('canvas');
    private _backStart = 0;
    private _backHeight = 0;
    private _backValid = false;
    private _needsFullRender = false;

    private _viewportValue: MinimapViewport = { top: 0, height: 0 };
    private _hovered: AbsoluteBlock | null = null;
    private _externalHovered: AbsoluteBlock | null = null;
    private _scrimAlpha = 1;
    private _scrimLastTick = 0;
    private _lastHostHeight = '';
    private _viewportHeight = 0;
    private _lastLiftTransform = '';
    private _lastLiftHeight = -1;
    private _lastAriaValue = -1;

    private readonly _thumbCache = new Map<string, ImageBitmap | 'failed'>();
    private readonly _inFlight = new Map<string, AbortController>();
    private _wanted = new Map<string, ThumbWant>();
    private _bandUrls = new Set<string>();

    private _drag: DragState | null = null;
    private _lastClickY = 0;
    private _lastClickTarget = 0;
    private _clickTimer: ReturnType<typeof setTimeout> | null = null;
    private _measureScheduled = false;
    private _flushScheduled = false;
    private _isDestroyed = false;

    constructor() {
        const elementRef = inject(ElementRef) as ElementRef<HTMLElement>;
        const destroyRef = inject(DestroyRef);

        this._hostEl = elementRef.nativeElement;

        const onScrollOrResize = () => this.scheduleMeasure();

        window.addEventListener('scroll', onScrollOrResize, { capture: true, passive: true });
        window.addEventListener('resize', onScrollOrResize, { passive: true });

        destroyRef.onDestroy(() => {
            this._isDestroyed = true;

            if (this._clickTimer !== null)
                clearTimeout(this._clickTimer);

            window.removeEventListener('scroll', onScrollOrResize, { capture: true });
            window.removeEventListener('resize', onScrollOrResize);

            for (const controller of this._inFlight.values())
                controller.abort();

            this._inFlight.clear();
            this._wanted.clear();

            for (const entry of this._thumbCache.values()) {
                if (entry !== 'failed')
                    entry.close();
            }

            this._thumbCache.clear();
        });

        document.fonts?.ready.then(() => {
            if (!this._isDestroyed)
                this.requestFullRender();
        });

        effect((onCleanup) => {
            const elements = this._observedElements();
            const resizeObserver = new ResizeObserver(() => this.scheduleMeasure());

            for (const element of elements)
                resizeObserver.observe(element);

            this.scheduleMeasure();

            onCleanup(() => resizeObserver.disconnect());
        });

        effect(() => {
            this.segments();
            this.scheduleMeasure();
        });

        effect((onCleanup) => {
            const canvas = this._canvasRef()?.nativeElement;

            if (!canvas)
                return;

            const resizeObserver = new ResizeObserver(() => {
                const rect = canvas.getBoundingClientRect();

                this._canvasSize.set({
                    w: Math.round(rect.width),
                    h: Math.round(rect.height)
                });
            });

            resizeObserver.observe(canvas);

            onCleanup(() => resizeObserver.disconnect());
        });

        effect((onCleanup) => {
            const rail = this._railRef()?.nativeElement;

            if (!rail)
                return;

            const onPointerDown = (event: PointerEvent) => this.onRailPointerDown(event);
            const onPointerMove = (event: PointerEvent) => this.onRailPointerMove(event);
            const onPointerUp = (event: PointerEvent) => this.onRailPointerUp(event);
            const onPointerCancel = (event: PointerEvent) => this.onRailPointerCancel(event);
            const onPointerEnter = () => this.onRailPointerEnter();
            const onPointerLeave = () => this.onRailPointerLeave();
            const onWheel = (event: WheelEvent) => this.onRailWheel(event);

            rail.addEventListener('pointerdown', onPointerDown);
            rail.addEventListener('pointermove', onPointerMove);
            rail.addEventListener('pointerup', onPointerUp);
            rail.addEventListener('pointercancel', onPointerCancel);
            rail.addEventListener('pointerenter', onPointerEnter);
            rail.addEventListener('pointerleave', onPointerLeave);
            rail.addEventListener('wheel', onWheel, { passive: false });

            onCleanup(() => {
                rail.removeEventListener('pointerdown', onPointerDown);
                rail.removeEventListener('pointermove', onPointerMove);
                rail.removeEventListener('pointerup', onPointerUp);
                rail.removeEventListener('pointercancel', onPointerCancel);
                rail.removeEventListener('pointerenter', onPointerEnter);
                rail.removeEventListener('pointerleave', onPointerLeave);
                rail.removeEventListener('wheel', onWheel);
            });
        });

        effect(() => {
            const headerHeight = this._headerHeight();

            this._hostEl.style.top = `${headerHeight}px`;
            this._lastHostHeight = '';
            this.scheduleFlush();
        });

        effect(() => {
            this._railRef();
            this._liftAnchorRef();

            this._lastAriaValue = -1;
            this._lastLiftTransform = '';
            this._lastLiftHeight = -1;

            this.scheduleFlush();
        });

        effect(() => {
            this._absBlocks();
            this._yBands();
            this._itemState();
            this._scale();
            this._canvasSize();

            this.requestFullRender();
        });

        effect(() => {
            const id = this.externalHoveredId();
            const { blocks } = this._absBlocks();

            untracked(() => {
                const block = id
                    ? blocks.find(b => b.block.id === id) ?? null
                    : null;

                if (this._externalHovered === block)
                    return;

                this._externalHovered = block;
                this.scheduleFlush();
            });
        });
    }

    private minimapContentHeight(): number {
        return this.yToMini(this._geometry().contentHeight);
    }

    private scrollFraction(): number {
        const geometry = this._geometry();
        const viewport = this._viewportValue;
        const scrollable = geometry.contentHeight - viewport.height;

        return scrollable > 1
            ? clamp(viewport.top / scrollable, 0, 1)
            : 0;
    }

    private slideOffset(): number {
        const maxSlide = Math.max(
            0,
            this.minimapContentHeight() - this._canvasSize().h);

        return this.scrollFraction() * maxSlide;
    }

    private indicatorHeight(): number {
        const viewport = this._viewportValue;
        const raw = this.yToMini(viewport.top + viewport.height) - this.yToMini(viewport.top);
        const canvasHeight = this._canvasSize().h;

        return clamp(
            raw,
            Math.min(MIN_INDICATOR_HEIGHT_PX, canvasHeight),
            canvasHeight);
    }

    private indicatorTop(): number {
        const travel = this.indicatorTravel();
        const raw = this.yToMini(this._viewportValue.top) - this.slideOffset();

        return clamp(raw, 0, Math.max(0, travel));
    }

    private indicatorTravel(): number {
        const canvasHeight = this._canvasSize().h;
        const usableHeight = Math.min(canvasHeight, this.minimapContentHeight());

        return usableHeight - this.indicatorHeight();
    }

    private scheduleMeasure(): void {
        if (this._measureScheduled || this._isDestroyed)
            return;

        this._measureScheduled = true;

        requestAnimationFrame(() => {
            this._measureScheduled = false;

            if (!this._isDestroyed)
                this.measure();
        });
    }

    private measure(): void {
        const host = this.contentHost();
        const hostRect = host.getBoundingClientRect();
        this._viewportHeight = this.resolveScrollViewportHeight();
        const header = this.stickyHeader();
        const headerRect = header?.getBoundingClientRect() ?? null;
        const windowHeight = window.innerHeight;

        const visibleTop = Math.max(hostRect.top, headerRect?.bottom ?? 0);
        const visibleBottom = Math.min(hostRect.bottom, windowHeight);

        this._viewportValue = {
            top: Math.max(0, visibleTop - hostRect.top),
            height: Math.max(0, visibleBottom - visibleTop)
        };

        const segmentGeometries: SegmentGeometry[] = this.segments().map(segment => {
            const sectionRect = segment.sectionEl?.getBoundingClientRect() ?? null;
            const contentRect = segment.contentEl?.getBoundingClientRect() ?? null;

            return {
                key: segment.key,
                sectionY: sectionRect ? sectionRect.top - hostRect.top : null,
                sectionHeight: sectionRect?.height ?? 0,
                contentY: contentRect ? contentRect.top - hostRect.top : null,
                contentWidth: contentRect?.width ?? 0,
                contentHeight: contentRect?.height ?? 0
            };
        });

        this._geometry.set({
            contentWidth: hostRect.width,
            contentHeight: hostRect.height,
            segments: segmentGeometries
        });

        this._isScrollableState.set(
            this._viewportValue.height > 0
            && hostRect.height > this._viewportValue.height + 1);

        if (headerRect)
            this._headerHeight.set(Math.ceil(headerRect.height));

        this.flush();
    }

    private requestFullRender(): void {
        this._needsFullRender = true;
        this.scheduleFlush();
    }

    private scheduleFlush(): void {
        if (this._flushScheduled || this._isDestroyed)
            return;

        this._flushScheduled = true;

        requestAnimationFrame(() => {
            this._flushScheduled = false;

            if (!this._isDestroyed)
                this.flush();
        });
    }

    private flush(): void {
        this.updateHostHeight();

        const { w, h } = untracked(() => this._canvasSize());

        if (w <= 0 || h <= 0)
            return;

        const slide = this.slideOffset();

        if (this._needsFullRender || !this._backValid || this.isBufferOutOfRange(slide, h)) {
            this._needsFullRender = false;
            this.renderBuffer(slide);
        }

        this.blit(slide);
        this.updateIndicator();
        this.paintLiftCard(slide);
    }

    private updateHostHeight(): void {
        const headerHeight = untracked(() => this._headerHeight());
        const available = Math.max(0, this._viewportHeight - headerHeight - HOST_BOTTOM_GAP_PX);

        const value = `${Math.floor(available)}px`;

        if (value !== this._lastHostHeight) {
            this._lastHostHeight = value;
            this._hostEl.style.height = value;
        }
    }

    private resolveScrollViewportHeight(): number {
        let element = this.contentHost().parentElement;

        while (element) {
            if (getComputedStyle(element).overflowY !== 'visible') {
                const clientHeight = element.clientHeight;

                if (clientHeight > 0)
                    return clientHeight;
            }

            element = element.parentElement;
        }

        return window.innerHeight;
    }

    private paintLiftCard(slide: number): void {
        const anchor = this._liftAnchorRef()?.nativeElement;
        const canvas = this._liftCanvasRef()?.nativeElement;

        if (!anchor || !canvas)
            return;

        const { w } = untracked(() => this._canvasSize());
        const top = Math.round(this.indicatorTop() * 2) / 2;
        const height = Math.max(1, Math.round(this.indicatorHeight() * 2) / 2);

        const isSunken = untracked(() => this.isRailHovered())
            && !untracked(() => this.isPressed());

        const scale = isSunken ? 1 : 1.04;
        const transform = `translateY(${top}px) scale(${scale})`;

        if (transform !== this._lastLiftTransform) {
            this._lastLiftTransform = transform;
            anchor.style.transform = transform;
        }

        if (height !== this._lastLiftHeight) {
            this._lastLiftHeight = height;
            anchor.style.height = `${height}px`;
        }

        const devicePixelRatio = window.devicePixelRatio || 1;
        const deviceWidth = Math.round(w * devicePixelRatio);
        const deviceHeight = Math.max(1, Math.round(height * devicePixelRatio));

        if (canvas.width !== deviceWidth)
            canvas.width = deviceWidth;

        if (canvas.height !== deviceHeight)
            canvas.height = deviceHeight;

        const ctx = canvas.getContext('2d');

        if (!ctx)
            return;

        ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
        ctx.clearRect(0, 0, w, height);

        if (!this._backValid)
            return;

        const sourceY = (slide + top - this._backStart) * devicePixelRatio;
        const sourceHeight = Math.min(
            deviceHeight,
            this._backCanvas.height - sourceY);

        if (sourceHeight <= 0)
            return;

        ctx.drawImage(
            this._backCanvas,
            0,
            sourceY,
            this._backCanvas.width,
            sourceHeight,
            0,
            0,
            w,
            sourceHeight / devicePixelRatio);

        const hovered = this._hovered ?? this._externalHovered;
        const mapScale = untracked(() => this._scale());

        if (hovered && mapScale > 0) {
            ctx.translate(0, -(slide + top));
            this.drawHoverOutline(ctx, hovered, mapScale);
        }
    }

    private updateIndicator(): void {
        const rail = this._railRef()?.nativeElement;

        if (!rail)
            return;

        const ariaValue = Math.round(this.scrollFraction() * 100);

        if (ariaValue !== this._lastAriaValue) {
            this._lastAriaValue = ariaValue;
            rail.setAttribute('aria-valuenow', String(ariaValue));
        }
    }

    private isBufferOutOfRange(slide: number, canvasHeight: number): boolean {
        const backEnd = this._backStart + this._backHeight;
        const minimapHeight = this.minimapContentHeight();

        if (slide < this._backStart || slide + canvasHeight > backEnd)
            return true;

        if (this._backStart > 0.5
            && slide - this._backStart < canvasHeight * BACK_BUFFER_REFILL_SCREENS)
            return true;

        if (backEnd < minimapHeight - 0.5
            && backEnd - (slide + canvasHeight) < canvasHeight * BACK_BUFFER_REFILL_SCREENS)
            return true;

        return false;
    }

    private renderBuffer(slide: number): void {
        const { w, h } = untracked(() => this._canvasSize());
        const scale = untracked(() => this._scale());

        if (w <= 0 || h <= 0 || scale <= 0) {
            this._backValid = false;
            return;
        }

        const devicePixelRatio = window.devicePixelRatio || 1;
        const minimapHeight = Math.max(1, this.minimapContentHeight());
        const bufferHeight = Math.min(minimapHeight, h * BACK_BUFFER_SCREENS);

        const backStart = clamp(
            slide - h * BACK_BUFFER_LEAD_SCREENS,
            0,
            Math.max(0, minimapHeight - bufferHeight));

        const deviceWidth = Math.round(w * devicePixelRatio);
        const deviceHeight = Math.max(1, Math.ceil(bufferHeight * devicePixelRatio));

        if (this._backCanvas.width !== deviceWidth)
            this._backCanvas.width = deviceWidth;

        if (this._backCanvas.height !== deviceHeight)
            this._backCanvas.height = deviceHeight;

        const ctx = this._backCanvas.getContext('2d');

        if (!ctx) {
            this._backValid = false;
            return;
        }

        this._backStart = backStart;
        this._backHeight = bufferHeight;
        this._backValid = true;

        ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
        ctx.clearRect(0, 0, w, bufferHeight);
        ctx.translate(0, -backStart);

        const fromContentY = this.miniToY(backStart - DRAW_OVERSCAN_PX);
        const toContentY = this.miniToY(backStart + bufferHeight + DRAW_OVERSCAN_PX);

        const { blocks, maxBlockHeight } = untracked(() => this._absBlocks());
        const itemState = untracked(() => this._itemState());
        const wanted = new Map<string, ThumbWant>();

        this._bandUrls = new Set<string>();

        const start = lowerBound(
            blocks,
            fromContentY - maxBlockHeight);

        for (let i = start; i < blocks.length; i++) {
            const absoluteBlock = blocks[i];

            if (absoluteBlock.y > toContentY)
                break;

            if (absoluteBlock.y + absoluteBlock.h < fromContentY)
                continue;

            if (absoluteBlock.block.kind === 'header')
                continue;

            this.drawBlock(
                ctx,
                absoluteBlock,
                scale,
                itemState,
                wanted);
        }

        for (const band of untracked(() => this._yBands())) {
            const bandTop = this.yToMini(band.contentY);
            const bandHeight = band.slotContent * scale + band.extraPx;

            if (bandTop + bandHeight < backStart - DRAW_OVERSCAN_PX || bandTop > backStart + bufferHeight + DRAW_OVERSCAN_PX)
                continue;

            if (band.kind === 'section') {
                this.drawSectionBar(
                    ctx,
                    band,
                    bandTop + band.topGapPx,
                    Math.max(SECTION_BAND_PX, bandHeight - band.topGapPx - SECTION_BAND_GAP_PX),
                    w);
            } else {
                this.drawMonthBar(ctx, band, bandTop, bandHeight, w);
            }
        }

        this.replaceWanted(wanted);
    }

    private blit(slide: number): void {
        const canvas = this._canvasRef()?.nativeElement;

        if (!canvas)
            return;

        const { w, h } = untracked(() => this._canvasSize());

        if (w <= 0 || h <= 0)
            return;

        const devicePixelRatio = window.devicePixelRatio || 1;
        const deviceWidth = Math.round(w * devicePixelRatio);
        const deviceHeight = Math.round(h * devicePixelRatio);

        if (canvas.width !== deviceWidth)
            canvas.width = deviceWidth;

        if (canvas.height !== deviceHeight)
            canvas.height = deviceHeight;

        const ctx = canvas.getContext('2d');

        if (!ctx)
            return;

        ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
        ctx.clearRect(0, 0, w, h);

        if (!this._backValid)
            return;

        const sourceY = (slide - this._backStart) * devicePixelRatio;
        const sourceHeight = Math.min(
            h * devicePixelRatio,
            this._backCanvas.height - sourceY);

        if (sourceHeight <= 0)
            return;

        ctx.drawImage(
            this._backCanvas,
            0,
            sourceY,
            this._backCanvas.width,
            sourceHeight,
            0,
            0,
            w,
            sourceHeight / devicePixelRatio);

        const isClearView = untracked(() => 
            this.isRailHovered() 
            && !this.isPressed() 
            && !this.isLiftHovered())

        const scrimTarget = isClearView ? 0 : 1;

        if (this._scrimAlpha !== scrimTarget) {
            const now = performance.now();
            const elapsed = this._scrimLastTick > 0 ? now - this._scrimLastTick : 16;
            const step = elapsed / SCRIM_FADE_MS;

            this._scrimLastTick = now;

            this._scrimAlpha = scrimTarget > this._scrimAlpha
                ? Math.min(scrimTarget, this._scrimAlpha + step)
                : Math.max(scrimTarget, this._scrimAlpha - step);

            this.scheduleFlush();
        } else {
            this._scrimLastTick = 0;
        }

        if (untracked(() => this._isScrollableState()) && this._scrimAlpha > 0.01) {
            const scrimHeight = Math.min(h, Math.max(0, this.minimapContentHeight() - slide));

            if (scrimHeight > 0) {
                ctx.globalAlpha = this._scrimAlpha;
                ctx.fillStyle = COLORS.scrimFill;
                ctx.fillRect(0, 0, w, scrimHeight);
                ctx.globalAlpha = 1;
            }
        }

        const hovered = this._hovered ?? this._externalHovered;
        const scale = untracked(() => this._scale());

        if (hovered && scale > 0) {
            ctx.translate(0, -slide);
            this.drawHoverOutline(ctx, hovered, scale);
        }
    }

    private blockRect(
        absoluteBlock: AbsoluteBlock,
        scale: number
    ): { x: number, y: number, w: number, h: number } {
        const x = absoluteBlock.xf * absoluteBlock.segmentWidth * scale;
        const y = this.yToMini(absoluteBlock.y);
        const rawWidth = absoluteBlock.wf * absoluteBlock.segmentWidth * scale;
        const rawHeight = this.yToMini(absoluteBlock.y + absoluteBlock.h) - y;

        if (absoluteBlock.block.kind === 'tile') {
            return {
                x,
                y,
                w: Math.max(1, rawWidth - 0.5),
                h: Math.max(1, rawHeight - 0.5)
            };
        }

        return {
            x,
            y,
            w: Math.max(1, rawWidth),
            h: Math.max(1, rawHeight - (rawHeight > 3 ? 1 : 0))
        };
    }

    private drawBlock(
        ctx: CanvasRenderingContext2D,
        absoluteBlock: AbsoluteBlock,
        scale: number,
        itemState: MinimapItemState,
        wanted: Map<string, ThumbWant> | null
    ): void {
        const block = absoluteBlock.block;
        const rect = this.blockRect(absoluteBlock, scale);
        const isSelected = block.id !== null && itemState.selectedIds.has(block.id);
        const isDimmed = block.id !== null && itemState.cutIds.has(block.id);

        if (isDimmed)
            ctx.globalAlpha = 0.4;

        if (block.kind === 'tile') {
            this.drawTile(
                ctx,
                absoluteBlock,
                rect,
                isSelected,
                wanted);
        } else {
            const thumb = block.thumbUrl
                ? this.resolveThumb(absoluteBlock, rect.h, rect.h, wanted)
                : null;

            this.drawRow(
                ctx,
                block,
                rect,
                isSelected,
                thumb,
                untracked(() => this._canvasSize()).w);
        }

        if (isDimmed)
            ctx.globalAlpha = 1;
    }

    private drawRow(
        ctx: CanvasRenderingContext2D,
        block: MinimapBlock,
        rect: { x: number, y: number, w: number, h: number },
        isSelected: boolean,
        thumb: ImageBitmap | null,
        canvasWidth: number
    ): void {
        const { x, y, w, h } = rect;
        const isFolder = block.kind === 'folder';

        if (h < 4) {
            roundRectPath(ctx, x, y, w, h, Math.min(1.5, h / 2));

            ctx.fillStyle = isSelected
                ? COLORS.selectedFill
                : isFolder ? COLORS.folderFill : COLORS.rowFill;

            ctx.fill();
            return;
        }

        if (isSelected) {
            ctx.fillStyle = COLORS.selectedRowFill;
            ctx.fillRect(0, y, canvasWidth, h);
        }

        const chipSize = h;

        if (isFolder) {
            this.drawFolderGlyph(ctx, x, y, chipSize);
        } else if (thumb) {
            const side = Math.min(thumb.width, thumb.height);

            ctx.drawImage(
                thumb,
                (thumb.width - side) / 2,
                (thumb.height - side) / 2,
                side,
                side,
                x,
                y,
                chipSize,
                h);
        } else if (block.thumbUrl) {
            ctx.fillStyle = COLORS.tileFill;
            ctx.fillRect(x, y, chipSize, h);
        } else {
            this.drawFileGlyph(ctx, x, y, chipSize);
        }

        let textLimitX = canvasWidth - 2;

        if (block.showCheckbox) {
            const checkboxSize = Math.min(6, Math.max(3, h - 2));
            const checkboxX = canvasWidth - checkboxSize - 2;

            this.drawCheckbox(
                ctx,
                checkboxX,
                y + (h - checkboxSize) / 2,
                checkboxSize,
                isSelected);

            textLimitX = checkboxX - 3;
        }

        if (!block.label)
            return;

        const fontSize = Math.min(7, h - 1);
        const textX = x + chipSize + 2;
        const maxWidth = textLimitX - textX;

        if (maxWidth < 4)
            return;

        ctx.font = `500 ${fontSize}px Inter, sans-serif`;

        const label = this.truncateToWidth(ctx, block.label, maxWidth);

        if (!label)
            return;

        ctx.fillStyle = COLORS.rowText;
        ctx.textBaseline = 'middle';

        ctx.fillText(
            label,
            textX,
            y + h / 2 + 0.5);

        ctx.textBaseline = 'alphabetic';
    }

    private truncateToWidth(
        ctx: CanvasRenderingContext2D,
        text: string,
        maxWidth: number
    ): string {
        const fullWidth = ctx.measureText(text).width;

        if (fullWidth <= maxWidth)
            return text;

        const averageCharWidth = fullWidth / text.length;
        let count = Math.floor((maxWidth - averageCharWidth) / averageCharWidth);

        if (count < 1)
            return '';

        let candidate = text.slice(0, count) + '…';

        while (count > 1 && ctx.measureText(candidate).width > maxWidth) {
            count = Math.max(1, Math.floor(count * 0.8));
            candidate = text.slice(0, count) + '…';
        }

        return candidate;
    }

    private drawFileGlyph(
        ctx: CanvasRenderingContext2D,
        x: number,
        y: number,
        size: number
    ): void {
        const paperWidth = size * 0.72;
        const paperX = x + (size - paperWidth) / 2;
        const fold = Math.max(1, size * 0.3);

        ctx.beginPath();
        ctx.moveTo(paperX, y);
        ctx.lineTo(paperX + paperWidth - fold, y);
        ctx.lineTo(paperX + paperWidth, y + fold);
        ctx.lineTo(paperX + paperWidth, y + size);
        ctx.lineTo(paperX, y + size);
        ctx.closePath();

        ctx.fillStyle = COLORS.fileGlyph;
        ctx.fill();
    }

    private drawFolderGlyph(
        ctx: CanvasRenderingContext2D,
        x: number,
        y: number,
        size: number
    ): void {
        const bodyHeight = size * 0.72;
        const bodyY = y + (size - bodyHeight) / 2;
        const tabHeight = Math.max(1, size * 0.18);
        const tabWidth = size * 0.45;

        ctx.fillStyle = COLORS.folderGlyph;
        ctx.fillRect(x, bodyY, tabWidth, tabHeight + 1);
        ctx.fillRect(x, bodyY + tabHeight, size, bodyHeight - tabHeight);
    }

    private drawCheckbox(
        ctx: CanvasRenderingContext2D,
        x: number,
        y: number,
        size: number,
        isChecked: boolean
    ): void {
        if (isChecked) {
            roundRectPath(ctx, x, y, size, size, 1);
            ctx.fillStyle = COLORS.selectedFill;
            ctx.fill();

            if (size >= 5) {
                ctx.strokeStyle = COLORS.checkboxCheck;
                ctx.lineWidth = 1;
                ctx.lineCap = 'round';
                ctx.lineJoin = 'round';
                ctx.beginPath();
                ctx.moveTo(x + size * 0.22, y + size * 0.55);
                ctx.lineTo(x + size * 0.45, y + size * 0.75);
                ctx.lineTo(x + size * 0.8, y + size * 0.28);
                ctx.stroke();
            }
        } else {
            roundRectPath(ctx, x + 0.5, y + 0.5, size - 1, size - 1, 1);
            ctx.strokeStyle = COLORS.checkboxBorder;
            ctx.lineWidth = 1;
            ctx.stroke();
        }
    }

    private drawTile(
        ctx: CanvasRenderingContext2D,
        absoluteBlock: AbsoluteBlock,
        rect: { x: number, y: number, w: number, h: number },
        isSelected: boolean,
        wanted: Map<string, ThumbWant> | null
    ): void {
        const { x, y, w, h } = rect;
        const radius = Math.min(1.5, h / 2);

        let imageX = x;
        let imageY = y;
        let imageW = w;
        let imageH = h;

        if (isSelected) {
            roundRectPath(ctx, x, y, w, h, radius);
            ctx.fillStyle = COLORS.selectedMat;
            ctx.fill();

            const mat = Math.max(2, Math.min(4, h * 0.14));

            imageX = x + mat;
            imageY = y + mat;
            imageW = Math.max(1, w - 2 * mat);
            imageH = Math.max(1, h - 2 * mat);
        }

        const useClip = Math.min(imageW, imageH) >= ROUND_CLIP_MIN_PX;
        const bitmap = this.resolveThumb(absoluteBlock, imageW, imageH, wanted);

        if (bitmap) {
            const blockRatio = imageW / imageH;
            const imageRatio = bitmap.width / bitmap.height;

            let sx = 0;
            let sy = 0;
            let sw = bitmap.width;
            let sh = bitmap.height;

            if (imageRatio > blockRatio) {
                sw = sh * blockRatio;
                sx = (bitmap.width - sw) / 2;
            } else {
                sh = sw / blockRatio;
                sy = (bitmap.height - sh) / 2;
            }

            if (isSelected)
                ctx.filter = 'grayscale(1) opacity(0.55)';

            if (useClip) {
                ctx.save();
                roundRectPath(ctx, imageX, imageY, imageW, imageH, radius);
                ctx.clip();
                ctx.drawImage(bitmap, sx, sy, sw, sh, imageX, imageY, imageW, imageH);
                ctx.restore();
            } else {
                ctx.drawImage(bitmap, sx, sy, sw, sh, imageX, imageY, imageW, imageH);
            }

            if (isSelected)
                ctx.filter = 'none';
        } else {
            if (useClip) {
                roundRectPath(ctx, imageX, imageY, imageW, imageH, radius);
                ctx.fillStyle = COLORS.tileFill;
                ctx.fill();
            } else {
                ctx.fillStyle = COLORS.tileFill;
                ctx.fillRect(imageX, imageY, imageW, imageH);
            }
        }

        if (isSelected) {
            roundRectPath(ctx, x + 1, y + 1, w - 2, h - 2, radius);
            ctx.strokeStyle = COLORS.selectedStroke;
            ctx.lineWidth = 2;
            ctx.stroke();
        }
    }

    private drawHoverOutline(
        ctx: CanvasRenderingContext2D,
        absoluteBlock: AbsoluteBlock,
        scale: number
    ): void {
        const rect = this.blockRect(absoluteBlock, scale);

        const width = absoluteBlock.block.kind === 'tile'
            ? rect.w
            : untracked(() => this._canvasSize()).w - 1;

        roundRectPath(
            ctx,
            rect.x + 0.5,
            rect.y + 0.5,
            Math.max(1, width - 1),
            Math.max(1, rect.h - 1),
            Math.min(1.5, rect.h / 2));

        ctx.strokeStyle = COLORS.hoverStroke;
        ctx.lineWidth = 1;
        ctx.stroke();
    }

    private drawSectionBar(
        ctx: CanvasRenderingContext2D,
        band: YBand,
        top: number,
        height: number,
        canvasWidth: number
    ): void {
        ctx.fillStyle = COLORS.divider;
        ctx.fillRect(0, top, canvasWidth, 1);

        ctx.font = SECTION_FONT;
        ctx.fillStyle = COLORS.sectionText;

        const previousSpacing = (ctx as unknown as { letterSpacing?: string }).letterSpacing;

        if (typeof previousSpacing === 'string')
            (ctx as unknown as { letterSpacing: string }).letterSpacing = '0.4px';

        ctx.fillText(
            band.label.toUpperCase(),
            4,
            top + height / 2 + 2.5,
            canvasWidth - 18);

        if (typeof previousSpacing === 'string')
            (ctx as unknown as { letterSpacing: string }).letterSpacing = previousSpacing;

        this.drawChevron(
            ctx,
            canvasWidth - 5,
            top + height / 2,
            band.isExpanded);
    }

    private drawChevron(
        ctx: CanvasRenderingContext2D,
        cx: number,
        cy: number,
        isExpanded: boolean
    ): void {
        ctx.strokeStyle = COLORS.chevron;
        ctx.lineWidth = 1;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.beginPath();

        if (isExpanded) {
            ctx.moveTo(cx - 2.5, cy - 1.25);
            ctx.lineTo(cx, cy + 1.25);
            ctx.lineTo(cx + 2.5, cy - 1.25);
        } else {
            ctx.moveTo(cx - 1.25, cy - 2.5);
            ctx.lineTo(cx + 1.25, cy);
            ctx.lineTo(cx - 1.25, cy + 2.5);
        }

        ctx.stroke();
    }

    private drawMonthBar(
        ctx: CanvasRenderingContext2D,
        band: YBand,
        top: number,
        height: number,
        canvasWidth: number
    ): void {
        ctx.fillStyle = COLORS.divider;
        ctx.fillRect(0, top, canvasWidth, 1);

        ctx.font = MONTH_FONT;
        ctx.fillStyle = COLORS.monthText;

        ctx.fillText(
            abbreviateMonthLabel(band.label),
            4,
            top + height / 2 + 2.5,
            canvasWidth - 8);
    }

    private resolveThumb(
        absoluteBlock: AbsoluteBlock,
        w: number,
        h: number,
        wanted: Map<string, ThumbWant> | null
    ): ImageBitmap | null {
        const url = absoluteBlock.block.thumbUrl;

        if (!url || Math.min(w, h) < THUMB_MIN_BLOCK_PX)
            return null;

        if (wanted)
            this._bandUrls.add(url);

        const cached = this._thumbCache.get(url);

        if (cached !== undefined) {
            if (wanted && cached !== 'failed') {
                this._thumbCache.delete(url);
                this._thumbCache.set(url, cached);
            }

            return cached === 'failed' ? null : cached;
        }

        if (wanted) {
            const want = wanted.get(url);

            if (want) {
                want.blocks.push(absoluteBlock);
            } else {
                wanted.set(url, {
                    blocks: [absoluteBlock],
                    centerY: this.yToMini(absoluteBlock.y + absoluteBlock.h / 2)
                });
            }
        }

        return null;
    }

    private replaceWanted(wanted: Map<string, ThumbWant>): void {
        this._wanted = wanted;
        this.pumpThumbQueue();
    }

    private pumpThumbQueue(): void {
        while (this._inFlight.size < THUMB_CONCURRENCY) {
            const url = this.pickNextWanted();

            if (!url)
                return;

            this.loadThumb(url);
        }
    }

    private pickNextWanted(): string | null {
        const viewCenter = this.slideOffset() + untracked(() => this._canvasSize()).h / 2;

        let bestUrl: string | null = null;
        let bestDistance = Infinity;

        for (const [url, want] of this._wanted) {
            if (this._thumbCache.has(url) || this._inFlight.has(url))
                continue;

            const distance = Math.abs(want.centerY - viewCenter);

            if (distance < bestDistance) {
                bestDistance = distance;
                bestUrl = url;
            }
        }

        return bestUrl;
    }

    private loadThumb(url: string): void {
        const controller = new AbortController();
        let timedOut = false;

        this._inFlight.set(url, controller);

        const timeout = setTimeout(
            () => {
                timedOut = true;
                controller.abort();
            },
            THUMB_LOAD_TIMEOUT_MS);

        fetch(url, { signal: controller.signal, priority: 'low' })
            .then(response => {
                if (!response.ok)
                    throw new Error(`minimap thumbnail failed with status ${response.status}`);

                return response.blob();
            })
            .then(blob => decodeThumb(blob))
            .then(bitmap => {
                if (this._isDestroyed) {
                    bitmap.close();
                    return;
                }

                this._thumbCache.set(url, bitmap);
                this.evictThumbsIfNeeded();
                this.paintThumb(url);
            })
            .catch(() => {
                if ((!controller.signal.aborted || timedOut) && !this._isDestroyed)
                    this._thumbCache.set(url, 'failed');
            })
            .finally(() => {
                clearTimeout(timeout);
                this._inFlight.delete(url);

                if (!this._isDestroyed)
                    this.pumpThumbQueue();
            });
    }

    private paintThumb(url: string): void {
        const want = this._wanted.get(url);

        if (!want || !this._backValid)
            return;

        const ctx = this._backCanvas.getContext('2d');
        const scale = untracked(() => this._scale());

        if (!ctx || scale <= 0)
            return;

        const devicePixelRatio = window.devicePixelRatio || 1;
        const itemState = untracked(() => this._itemState());

        ctx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
        ctx.translate(0, -this._backStart);

        const fromContentY = this.miniToY(this._backStart - DRAW_OVERSCAN_PX);
        const toContentY = this.miniToY(this._backStart + this._backHeight + DRAW_OVERSCAN_PX);

        for (const absoluteBlock of want.blocks) {
            if (absoluteBlock.y + absoluteBlock.h < fromContentY || absoluteBlock.y > toContentY)
                continue;

            this.drawBlock(
                ctx,
                absoluteBlock,
                scale,
                itemState,
                null);
        }

        this.scheduleFlush();
    }

    private evictThumbsIfNeeded(): void {
        if (this._thumbCache.size <= THUMB_CACHE_MAX)
            return;

        const target = Math.floor(THUMB_CACHE_MAX * 0.9);

        for (const [url, entry] of this._thumbCache) {
            if (this._thumbCache.size <= target)
                break;

            if (this._wanted.has(url) || this._bandUrls.has(url))
                continue;

            if (entry !== 'failed')
                entry.close();

            this._thumbCache.delete(url);
        }
    }

    private onRailPointerDown(event: PointerEvent): void {
        if (event.button !== 0)
            return;

        const canvas = this._canvasRef()?.nativeElement;
        const rail = this._railRef()?.nativeElement;

        if (!canvas || !rail)
            return;

        const y = event.clientY - canvas.getBoundingClientRect().top;
        const indicatorTop = this.indicatorTop();
        const indicatorHeight = this.indicatorHeight();
        const isInsideViewport = y >= indicatorTop && y <= indicatorTop + indicatorHeight;

        rail.setPointerCapture(event.pointerId);

        this._drag = {
            pointerId: event.pointerId,
            startY: y,
            grabOffset: isInsideViewport
                ? y - indicatorTop
                : indicatorHeight / 2,
            moved: false,
            expectedTop: this._viewportValue.top
        };

        if (isInsideViewport && untracked(() => this._isScrollableState())) {
            this.isPressed.set(true);
            this.scheduleFlush();
        }

        event.preventDefault();
    }

private onRailPointerMove(event: PointerEvent): void {
        const canvas = this._canvasRef()?.nativeElement;

        if (!canvas)
            return;

        const y = event.clientY - canvas.getBoundingClientRect().top;

        if (this._drag && this._drag.pointerId === event.pointerId) {
            if (!this._drag.moved && Math.abs(y - this._drag.startY) < DRAG_THRESHOLD_PX)
                return;

            this._drag.moved = true;

            if (untracked(() => this._isScrollableState())) {
                this.isDragging.set(true);
                this.isPressed.set(true);
                this.setHovered(null);
                this.tooltip.set(null);

                this.dragToIndicatorTop(
                    this._drag,
                    y - this._drag.grabOffset);
            }

            return;
        }

        this.updateHover(event, y);
    }

    private onRailPointerUp(event: PointerEvent): void {
        const drag = this._drag;

        if (!drag || drag.pointerId !== event.pointerId)
            return;

        this._drag = null;
        this.isDragging.set(false);
        this.isPressed.set(false);
        this.scheduleFlush();

        if (drag.moved)
            return;

        const canvas = this._canvasRef()?.nativeElement;
        const scale = untracked(() => this._scale());

        if (!canvas || scale <= 0)
            return;

        const rect = canvas.getBoundingClientRect();
        const y = event.clientY - rect.top;
        const isCtrl = event.ctrlKey || event.metaKey;
        const isShift = event.shiftKey;

        if (isCtrl || isShift) {
            const xFraction = (event.clientX - rect.left) / Math.max(1, rect.width);
            const contentY = this.miniToY(y + this.slideOffset());
            const hit = this.hitTest(contentY, xFraction);

            if (hit?.block.id) {
                const ref: MinimapItemRef = {
                    id: hit.block.id,
                    kind: hit.block.kind
                };

                if (isCtrl)
                    this.itemCtrlClicked.emit(ref);
                else
                    this.itemShiftClicked.emit(ref);
            }

            return;
        }

        const targetContentY = this.miniToY(y + this.slideOffset());

        const isDoubleClick =
            this._clickTimer !== null
            && Math.abs(y - this._lastClickY) < DOUBLE_CLICK_MOVE_PX;

        if (this._clickTimer !== null) {
            clearTimeout(this._clickTimer);
            this._clickTimer = null;
        }

        if (isDoubleClick) {
            this.scrollToCenterContentY(this._lastClickTarget, 'instant');
            return;
        }

        this._lastClickY = y;
        this._lastClickTarget = targetContentY;

        this._clickTimer = setTimeout(
            () => {
                this._clickTimer = null;

                if (!this._isDestroyed)
                    this.scrollToCenterContentY(this._lastClickTarget, 'smooth');
            },
            DOUBLE_CLICK_MS);
    }

    private onRailPointerCancel(event: PointerEvent): void {
        if (this._drag?.pointerId !== event.pointerId)
            return;

        this._drag = null;
        this.isDragging.set(false);
        this.isPressed.set(false);
        this.scheduleFlush();
    }

    private onRailPointerEnter(): void {
        if (untracked(() => this.isRailHovered()))
            return;

        this.isRailHovered.set(true);
        this.scheduleFlush();
    }

    private onRailPointerLeave(): void {
        this.isRailHovered.set(false);
        this.setHovered(null);
        this.tooltip.set(null);
        this.scheduleFlush();
    }

    private onRailWheel(event: WheelEvent): void {
        event.preventDefault();

        const delta = event.deltaMode === WheelEvent.DOM_DELTA_LINE
            ? event.deltaY * WHEEL_LINE_HEIGHT_PX
            : event.deltaY;

        this.scrollByContentDelta(delta, 'instant');
    }

    private setHovered(hovered: AbsoluteBlock | null): void {
        if (this._hovered === hovered)
            return;

        this._hovered = hovered;

        this.itemHovered.emit(hovered?.block.id
            ? { id: hovered.block.id, kind: hovered.block.kind }
            : null);

        this.scheduleFlush();
    }

    private updateHover(event: PointerEvent, y: number): void {
        const canvas = this._canvasRef()?.nativeElement;
        const scale = untracked(() => this._scale());

        if (!canvas || scale <= 0) {
            this.setHovered(null);
            this.tooltip.set(null);

            if (untracked(() => this.isLiftHovered())) {
                this.isLiftHovered.set(false);
                this.scheduleFlush();
            }

            return;
        }

        const indicatorTop = this.indicatorTop();
        const indicatorHeight = this.indicatorHeight();
        const overLift = y >= indicatorTop && y <= indicatorTop + indicatorHeight;

        if (untracked(() => this.isLiftHovered()) !== overLift) {
            this.isLiftHovered.set(overLift);
            this.scheduleFlush();
        }

        const rect = canvas.getBoundingClientRect();
        const xFraction = (event.clientX - rect.left) / Math.max(1, rect.width);
        const contentY = this.miniToY(y + this.slideOffset());
        const hit = this.hitTest(contentY, xFraction);

        this.setHovered(hit);

        if (hit?.block.label) {
            const top = clamp(
                this.yToMini(hit.y + hit.h / 2) - this.slideOffset(),
                8,
                Math.max(8, untracked(() => this._canvasSize()).h - 8));

            this.tooltip.set({
                label: hit.block.label,
                top: Math.round(top)
            });
        } else {
            this.tooltip.set(null);
        }
    }

    private hitTest(contentY: number, xFraction: number): AbsoluteBlock | null {
        const { blocks, maxBlockHeight } = untracked(() => this._absBlocks());
        const start = lowerBound(
            blocks,
            contentY - maxBlockHeight);

        let best: AbsoluteBlock | null = null;

        for (let i = start; i < blocks.length; i++) {
            const absoluteBlock = blocks[i];

            if (absoluteBlock.y > contentY)
                break;

            if (contentY > absoluteBlock.y + absoluteBlock.h)
                continue;

            if (absoluteBlock.block.kind === 'header')
                continue;

            if (absoluteBlock.block.kind === 'tile'
                && (xFraction < absoluteBlock.xf || xFraction > absoluteBlock.xf + absoluteBlock.wf))
                continue;

            best = absoluteBlock;
        }

        return best;
    }

    private dragToIndicatorTop(
        drag: DragState,
        indicatorTop: number
    ): void {
        const travel = this.indicatorTravel();

        if (travel <= 0)
            return;

        const geometry = untracked(() => this._geometry());
        const viewport = this._viewportValue;
        const fraction = clamp(indicatorTop / travel, 0, 1);
        const targetTop = fraction * (geometry.contentHeight - viewport.height);
        const delta = targetTop - drag.expectedTop;

        drag.expectedTop = targetTop;

        this.scrollByContentDelta(delta, 'instant');
    }

    private scrollToCenterContentY(
        contentY: number,
        behavior: ScrollBehavior
    ): void {
        const geometry = untracked(() => this._geometry());
        const viewport = this._viewportValue;

        const targetTop = clamp(
            contentY - viewport.height / 2,
            0,
            Math.max(0, geometry.contentHeight - viewport.height));

        this.scrollByContentDelta(targetTop - viewport.top, behavior);
    }

    private scrollByContentDelta(
        delta: number,
        behavior: ScrollBehavior
    ): void {
        if (Math.abs(delta) < 0.5)
            return;

        const scroller = this.findScroller();

        scroller.scrollBy({ top: delta, behavior });
    }

    private findScroller(): HTMLElement | Window {
        let element = this.contentHost().parentElement;

        while (element) {
            const style = getComputedStyle(element);

            if ((style.overflowY === 'auto' || style.overflowY === 'scroll')
                && element.scrollHeight > element.clientHeight + 1)
                return element;

            element = element.parentElement;
        }

        return window;
    }
}
