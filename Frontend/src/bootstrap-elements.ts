import { createApplication } from '@angular/platform-browser';
import { enableProdMode, importProvidersFrom, provideExperimentalZonelessChangeDetection } from '@angular/core';
import { createCustomElement } from '@angular/elements';
import { BoxWidgetComponent } from './app/external-access/box-widget/box-widget.component';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { BrowserAnimationsModule, provideAnimations } from '@angular/platform-browser/animations';
import { provideMarkdown } from 'ngx-markdown';
import { ToastrModule } from 'ngx-toastr';

enableProdMode();

const bootstrap = async () => {
  // Create a mini application just for custom elements
  const appRef = await createApplication({
    providers: [
      provideExperimentalZonelessChangeDetection(),
      provideHttpClient(withFetch()),
      importProvidersFrom([
        ToastrModule.forRoot(),
        BrowserAnimationsModule,
      ]),
      provideAnimations(),
      provideMarkdown()
    ]
  });
  
  const injector = appRef.injector;
  
  // Create the custom element
  const PliskshareElement = createCustomElement(BoxWidgetComponent, { injector });
  
  // Register the custom element with the browser
  if (!customElements.get('plikshare-box-widget')) {
    customElements.define('plikshare-box-widget', PliskshareElement);
  }
};

// Run the bootstrap function
bootstrap().catch(err => console.error('Error bootstrapping the elements app', err));
