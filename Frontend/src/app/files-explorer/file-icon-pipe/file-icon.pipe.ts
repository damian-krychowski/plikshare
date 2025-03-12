import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'fileIcon',
  standalone: true
})
export class FileIconPipe implements PipeTransform {

  transform(value: any, ...args: unknown[]): unknown {
    const ext = value 
          ? value.toString().toLowerCase().split('.').pop()
          : '';

    switch (ext) {
      case 'bmp':
      case 'jpg':
      case 'png':
      case 'gif':
      case 'svg':
      case 'jpeg':
      case 'ico':
      case 'tif':
      case 'tiff':
      case 'webp':
        return 'nucleo-image';
      case 'pdf':
      case 'doc':
      case 'docx':
      case 'xls':
      case 'xlsx':
      case 'ppt':
      case 'pptx':
      case 'txt':
      case 'md':
        return 'nucleo-file-txt';
      case 'zip':
      case 'rar':
      case '7z':
      case 'tar':
        return 'nucleo-file-zip';
      case 'mp3':
      case 'wav':
      case 'ogg':
      case 'flac':
        return 'nucleo-file-music';
      case 'mp4':
      case 'avi':
      case 'mkv':
      case 'mov':
      case 'wmv':
      case 'flv':
      case 'webm':
        return 'nucleo-video';
      default:
        return 'nucleo-file';
    }
  }
}
