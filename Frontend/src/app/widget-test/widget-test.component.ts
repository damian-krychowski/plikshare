import { Component, effect, ElementRef, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { WidgetsApi } from '../services/widgets.api';
import { ConfigCardComponent } from '../shared/config-card/config-card.component';
import { ActionTextButtonComponent } from '../shared/buttons/action-text-btn/action-text-btn.component';
import { ActionButtonComponent } from '../shared/buttons/action-btn/action-btn.component';

type HeightMode = 'fixed' | 'page';
type LogKind = 'info' | 'ok' | 'err';

@Component({
    selector: 'app-widget-test',
    imports: [
        FormsModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        ConfigCardComponent,
        ActionTextButtonComponent,
        ActionButtonComponent
    ],
    templateUrl: './widget-test.component.html',
    styleUrl: './widget-test.component.scss'
})
export class WidgetTestComponent implements OnInit {
    @ViewChild('stage', { static: true }) stageRef!: ElementRef<HTMLElement>;

    url = signal<string>('');
    heightMode = signal<HeightMode>('fixed');
    fixedHeight = signal<number>(600);

    hasWidget = signal<boolean>(false);
    collapsed = signal<boolean>(false);
    log = signal<{ message: string, kind: LogKind }[]>([]);

    private _scriptsLoaded = false;
    private _autoRenderReady = false;
    private _autoRenderTimer: ReturnType<typeof setTimeout> | null = null;

    constructor(
        private _widgetsApi: WidgetsApi,
        private _route: ActivatedRoute) {

        effect(() => {
            this.url();
            this.heightMode();
            this.fixedHeight();
            this.scheduleAutoRender();
        });
    }

    private scheduleAutoRender(): void {
        if (!this._autoRenderReady)
            return;

        if (this._autoRenderTimer)
            clearTimeout(this._autoRenderTimer);

        this._autoRenderTimer = setTimeout(() => this.render(), 400);
    }

    ngOnInit(): void {
        const url = this._route.snapshot.queryParamMap.get('url');

        if (url)
            this.url.set(url);
    }

    async render(): Promise<void> {
        const url = this.url().trim();

        if (!url) {
            this.append('Enter a box link URL first.', 'err');
            return;
        }

        this._autoRenderReady = true;

        try {
            await this.loadScripts();

            const stage = this.stageRef.nativeElement;
            stage.innerHTML = '';

            const element = document.createElement('plikshare-box-widget');
            element.setAttribute('url', url);
            element.style.display = 'block';

            if (this.heightMode() === 'fixed') {
                stage.style.height = `${this.fixedHeight()}px`;
                stage.style.overflow = 'hidden';
                element.style.height = '100%';
            } else {
                stage.style.height = '';
                stage.style.overflow = '';
                element.style.height = '';
            }

            stage.appendChild(element);
            this.hasWidget.set(true);

            this.append(`Rendered widget for ${url}`, 'ok');
        } catch (error: any) {
            this.append(`Render failed: ${error?.message ?? error}`, 'err');
        }
    }

    clear(): void {
        this._autoRenderReady = false;

        if (this._autoRenderTimer)
            clearTimeout(this._autoRenderTimer);

        this.stageRef.nativeElement.innerHTML = '';
        this.stageRef.nativeElement.style.height = '';
        this.stageRef.nativeElement.style.overflow = '';
        this.hasWidget.set(false);
        this.log.set([]);
    }

    private async loadScripts(): Promise<void> {
        if (this._scriptsLoaded)
            return;

        this.append('Fetching /api/widgets/scripts ...');

        const markup = await this._widgetsApi.getWidgetScripts();

        const template = document.createElement('template');
        template.innerHTML = markup;

        const nodes = Array.from(template.content.querySelectorAll('link, script'));

        if (nodes.length === 0)
            throw new Error('scripts endpoint returned no tags (elements bundle missing?)');

        for (const node of nodes) {
            if (node.tagName === 'LINK') {
                document.head.appendChild(node.cloneNode(true));
                this.append(`Loaded stylesheet: ${node.getAttribute('href')}`, 'ok');
                continue;
            }

            await new Promise<void>((resolve, reject) => {
                const script = document.createElement('script');

                for (const attr of Array.from(node.attributes))
                    script.setAttribute(attr.name, attr.value);

                script.onload = () => {
                    this.append(`Loaded script: ${script.src}`, 'ok');
                    resolve();
                };
                script.onerror = () => reject(new Error(`failed to load ${script.src}`));

                document.head.appendChild(script);
            });
        }

        await customElements.whenDefined('plikshare-box-widget');
        this.append('Custom element registered.', 'ok');

        this._scriptsLoaded = true;
    }

    private append(message: string, kind: LogKind = 'info'): void {
        this.log.update(entries => [...entries, { message, kind }]);
    }
}
