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

    private isOsFileDrag(evt: DragEvent): boolean {
      const types = evt.dataTransfer?.types;
      if (!types) return false;
      for (let i = 0; i < types.length; i++) {
        if (types[i] === 'Files') return true;
      }
      return false;
    }

    @HostListener('dragenter', ['$event']) onDragEnter(evt: DragEvent) {
      if (!this.isOsFileDrag(evt)) return;
      evt.preventDefault();
      this.fileOver = true;
    }

    @HostListener('dragover', ['$event']) onDragOver(evt: DragEvent) {
      if (!this.isOsFileDrag(evt)) return;
      evt.preventDefault();
      this.fileOver = true;
    }

    @HostListener('dragleave', ['$event']) public onDragLeave(evt: DragEvent) {
      if (!this.isOsFileDrag(evt)) return;
      evt.preventDefault();
      this.fileOver = false;
    }

    @HostListener('drop', ['$event']) public ondrop(evt: DragEvent) {
      if (!this.isOsFileDrag(evt)) return;
      evt.preventDefault();
      this.fileOver = false;
      const files = evt.dataTransfer?.files;
      if (files && files.length > 0) {
        this.filesDropped.emit(files);
      }
    }
  }
  