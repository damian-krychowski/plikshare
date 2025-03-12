import { Directive, OnInit, Input, Renderer2, HostListener, ElementRef } from "@angular/core";

@Directive({
    selector: '[appSecureInput]',
    standalone: true
})
export class SecureInputDirective implements OnInit {
    private _isHidden = true;
    private _button: HTMLButtonElement | null = null;
    private _input: HTMLInputElement | null = null;
    private _icon: HTMLSpanElement | null = null;
    private _isPassword: boolean = false;
    private _ariaLabel = 'Toggle visibility';
    private _container: HTMLDivElement | null = null;

    constructor(
        private el: ElementRef,
        private renderer: Renderer2
    ) { }

    ngOnInit() {
        this.createContainer();
        this.setupInputElement();
        this.createToggleButton();
    }

    private createContainer() {
        this._container = this.renderer.createElement('div');
        this.renderer.addClass(this._container, 'secure-input');
        
        const parent = this.renderer.parentNode(this.el.nativeElement);
        this.renderer.insertBefore(parent, this._container, this.el.nativeElement);
        
        this.renderer.appendChild(this._container, this.el.nativeElement);
      }

    private setupInputElement() {
        this._input = this.el.nativeElement;
        this._isPassword = this._input?.getAttribute('type') === 'password';

        this.renderer.addClass(this._input, 'secure-input--on');   
        
        if(!this._isPassword){
            this.renderer.setAttribute(this._input, 'autocomplete', 'off');
        }
    }

    private createToggleButton() {
        this._button = this.renderer.createElement('button');
        this.renderer.setAttribute(this._button, 'type', 'button');
        this.renderer.setAttribute(this._button, 'aria-label', this._ariaLabel);
        this.renderer.addClass(this._button, 'secure-input__btn');

        this._icon = this.renderer.createElement('i');
        this.renderer.addClass(this._icon , 'icon');
        this.renderer.addClass(this._icon , 'icon-xl');
        this.renderer.addClass(this._icon , 'icon-nucleo-eye');
        this.renderer.setStyle(this._icon , 'opacity', '0.7');

        this.renderer.appendChild(this._button, this._icon );
        this.renderer.listen(this._button, 'click', (event) => this.toggleVisibility(event));

        const parent = this.renderer.parentNode(this.el.nativeElement);
        this.renderer.insertBefore(parent, this._button, this.el.nativeElement.nextSibling);
    }

    @HostListener('input')
    onInput() {
        this.updateButtonState();
    }

    private toggleVisibility(event: Event) {
        event.preventDefault();
        event.stopPropagation();
        this._isHidden = !this._isHidden;

        this.updateInputState();
        this.updateButtonState();
        this.updateIconState();
    }

    private updateInputState() {
        if(this._isHidden) {
            this.renderer.addClass(this._input, 'secure-input--on');
            
            if(this._isPassword) {
                this.renderer.setAttribute(this._input, 'type', 'password');
            }
        } else {
            this.renderer.removeClass(this._input, 'secure-input--on');

            if(this._isPassword) {
                this.renderer.setAttribute(this._input, 'type', 'text');
            }
        }
    }

    private updateButtonState() {
        this.renderer.setAttribute(this._button, 'aria-pressed', (!this._isHidden).toString());
    }

    private updateIconState() {
        this.renderer.removeClass(this._icon , 'icon-nucleo-eye');
        this.renderer.removeClass(this._icon , 'icon-nucleo-eye-slash');
        this.renderer.addClass(this._icon , this._isHidden ? 'icon-nucleo-eye' : 'icon-nucleo-eye-slash')
    }
}