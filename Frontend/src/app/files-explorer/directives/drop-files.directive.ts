import {
    Directive,
    Output,
    Input,
    EventEmitter,
    HostBinding,
    HostListener,
    output
  } from '@angular/core';
  
  @Directive({
    selector: '[appDropFiles]',
    standalone: true
  })
  export class DropFilesDirective {
    @HostBinding('class.drop-area--drag-over') fileOver: boolean = false;

    filesDropped = output<any>();
  
    @HostListener('dragenter', ['$event']) onDragOver(evt: any) {
      evt.preventDefault();
      this.fileOver = true;
    }
  
    @HostListener('dragleave', ['$event']) public onDragLeave(evt: any) {
      evt.preventDefault();
      this.fileOver = false;
    }
  
    @HostListener('drop', ['$event']) public ondrop(evt:any) {
      evt.preventDefault();    
      this.fileOver = false;
      let files = evt.dataTransfer.files;
      if (files.length > 0) {
        this.filesDropped.emit(files);
      }
    }
  }
  