import { Component, DestroyRef, ElementRef, Signal, ViewChild, computed, effect, inject, input, output, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { DomSanitizer, SafeHtml } from "@angular/platform-browser";
import { SelectAllTextDirective } from "../select-all-text.directive";
import { MarqueeOnTruncateDirective } from "../marquee-on-truncate.directive";
import { getNameWithHighlight } from "../name-with-highlight";

@Component({
    selector: 'app-editable-txt',
    imports: [
        FormsModule,
        SelectAllTextDirective,
        MarqueeOnTruncateDirective
    ],
    templateUrl: './editable-txt.component.html',
    styleUrl: './editable-txt.component.scss'
})
export class EditableTxtComponent {
    isEditing = input.required<boolean>();
    text = input.required<string>();
    textToDisplay = input<string>();
    canEdit = input(true);
    highlightPhrase = input<string>('');

    visibleText = computed(() => this.textToDisplay() ?? this.text());

    // True only when the caller actually wants a highlighted match. Used by
    // the template to fork between cheap text interpolation and the heavier
    // [innerHTML] path — see template for the @if branches.
    hasHighlight = computed(() => this.highlightPhrase().length > 0);

    // Only built when hasHighlight is true (template gates the binding). The
    // path involves escapeHtml (5 regex replaces over the full text),
    // string concatenation, and bypassSecurityTrustHtml wrapping — all of
    // which Angular then re-parses into DOM nodes via [innerHTML]. For the
    // common case (no search active), text interpolation is several times
    // cheaper and good enough.
    displayHtml: Signal<SafeHtml> = computed(() => {
        const text = this.visibleText();
        const phrase = this.highlightPhrase().toLowerCase();
        const html = getNameWithHighlight(text, phrase);
        return this._sanitizer.bypassSecurityTrustHtml(html);
    });

    valueChange = output<string>();
    editingStopped = output<void>();
    editingStarted = output<void>();

    public newValue: string = '';

    @ViewChild('mirrorSpan') mirrorSpan!: ElementRef<HTMLSpanElement>;
    inputWidth = signal(50);

    private _hostEl = inject(ElementRef<HTMLElement>);

    // The click event for a single mouse-press fires as a SEPARATE event
    // after mouseup, so preventDefault on mousedown does NOT block click on
    // its own. We need a paired click-capture listener that swallows the
    // upcoming click, gated by this flag.
    private _suppressNextClick = false;

    // Capture-phase document-level mousedown listener. Fires BEFORE the
    // event reaches its actual target, so we get first dibs on every
    // mousedown anywhere on the page.
    //
    // When the user clicks outside our editor while editing:
    //   - preventDefault blocks the focus shift to the click target
    //   - stopPropagation keeps the mousedown from reaching the target's
    //     own handler
    //   - we arm _suppressNextClick so the partner click-capture listener
    //     can also swallow the click event that follows mouseup
    //   - save() commits the rename
    //
    // Why always-attached (registered in constructor, not in an effect):
    // an effect-based register/unregister races with the very click event
    // we're trying to suppress. save() flips isEditing → false, the effect
    // re-runs in the next microtask and detaches the click listener —
    // potentially BEFORE the click event fires. With the listener always
    // attached, the `_suppressNextClick` flag survives the state flip.
    private _outsideMousedown = (event: MouseEvent) => {
        if (!this.isEditing()) return;

        const target = event.target;
        // Check against the actual `.txt-editor` element rather than the
        // <app-editable-txt> host: the host is `flex: 1 1 auto` and stretches
        // across whatever empty space the parent gives it, so `host.contains`
        // would treat clicks on that empty padding as "inside" — even though
        // they're nowhere near the input. closest() walks up from the click
        // target until it finds .txt-editor (input/mirror live inside it);
        // null means the click landed outside the actual editor box.
        if (target instanceof Element && target.closest('.txt-editor') !== null) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        this._suppressNextClick = true;
        this.save();
    };

    // Partner listener — swallows the click event paired with the mousedown
    // we intercepted above. Without this, the sibling item's `(click)`
    // handler would still run (preventDefault on mousedown only blocks the
    // focus shift, not the click event itself).
    private _outsideClick = (event: MouseEvent) => {
        if (!this._suppressNextClick) return;
        this._suppressNextClick = false;
        event.preventDefault();
        event.stopPropagation();
    };

    constructor(private _sanitizer: DomSanitizer) {
        effect(() => {
            if(this.isEditing()) {
                this.newValue = this.text();
                this.adjustInputWidth();
            }
        });

        // Always-attached document listeners — register/unregister tied to
        // isEditing would race with the very click event we want to suppress
        // (effect detaches the listeners in a microtask before click fires).
        // Each listener early-returns when isEditing is false; signal read
        // is sub-microsecond so the overhead is negligible even with many
        // editable-txt instances on the page.
        document.addEventListener('mousedown', this._outsideMousedown, true);
        document.addEventListener('click', this._outsideClick, true);

        inject(DestroyRef).onDestroy(() => {
            document.removeEventListener('mousedown', this._outsideMousedown, true);
            document.removeEventListener('click', this._outsideClick, true);
        });
    }

    public adjustInputWidth() {
        setTimeout(() => {
            const spanWidth = this.mirrorSpan?.nativeElement.offsetWidth ?? 0;
            this.inputWidth.set(spanWidth > 50 ? spanWidth + 10 : 50); // Use 50 as the min-width or adjust as needed
        });
    }
    
    public startEditing() {
        this.editingStarted.emit();
    }

    public save() {
        if(!this.isEditing()) {
            return;
        }

        const isValueChanged = this.text() !== this.newValue;

        if (isValueChanged) {
            this.valueChange.emit(this.newValue);
        }

        this.editingStopped.emit();
    }

    public cancelEditing() {
        this.editingStopped.emit();
    }
}