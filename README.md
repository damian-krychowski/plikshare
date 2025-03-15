<img alt="plikshare_dashboard" src="https://github.com/damian-krychowski/plikshare/blob/main/assets/dashboard_printscreen.png">

# PlikShare

PlikShare is a self-hosted file sharing application that allows you to securely share files with others.


## Features

- Self-hosted file sharing solution
- Secure file upload and download
- User management
- Access control
- Files Encryption
- Easy deployment


## Hey Claude, how to pronounce it?
![claude_pronunciation](https://github.com/damian-krychowski/plikshare/blob/main/assets/how_to_pronounce.png)

I double-checked - he made no mistake this time. It's "pleek-share" (plēk-shâr)!

Where "Plik" in ancient Polish means "file".

(Yeap, it's hard to find good domain name these times 🥲)


## How to install

All the necessery instructions can be found here: https://plikshare.com/download

Docker image is available here: https://hub.docker.com/r/damiankrychowski/plikshare


## How to setup

To start using PlikShare you need to do two things: 
- Configure an email client (so that PlikShare can send notifications, confirm user emails etc.)
- Configure a storage

<video src='https://github.com/user-attachments/assets/4a599cb3-13f4-4676-89bb-6734358bee25' width=180></video>


## How to upload files
The fastes way is to create zip archive out of the files you want to upload and then use PlikShare bulk upload feature!

<video src='https://github.com/user-attachments/assets/3752a954-8d33-4d89-9ba1-69b922fb45e3' width=180></video>


## How to download files
My favorite way to download files from PlikShare is to use explorer tree view - there you pick files and folders you want to have in your final archive and download them at once.

<video src='https://github.com/user-attachments/assets/8839b973-3aac-4e25-8a77-13ecaaec204c' width=180></video>


## How to share files
PlikShare allows you to create "boxes" - a layer that connects your folders to the external world. These boxes give you control over how others interact with your selected folders. With boxes, you can:

- Invite team members to collaborate
- Create anonymous links with various permission levels
- Set up read-only boxes where others can only view content
- Create upload-only boxes where users can add files without seeing existing content

This structured approach provides a secure and flexible way to share your files while maintaining precise control over access permissions.

<video src='https://github.com/user-attachments/assets/87fe5b20-23fa-409f-bfef-3d7f787702e8' width=180></video>


## How to preview files
PlikShare includes a built-in preview function for various file formats. You can open videos, audio files, text files, PDFs, and it can render markdown files directly in the browser.

I couldn't resist implementing ZIP file browsing capabilities as well. This feature allows you to navigate through ZIP archives and even preview files contained within them without having to extract the archive first.

<video src='https://github.com/user-attachments/assets/8ad6da6d-1f71-4794-857f-d0333acd7f52' width=180></video>


## Markdown Files
PlikShare can render markdown files and it supports [mermaid diagrams](https://mermaid.js.org/intro/). Markdown files are also the only files in PlikShare (for now!) that can be directly edited.

Why? It's connected with my development plans for the app. My long-term goal is to integrate PlikShare seamlessly with AI such as ChatGPT, Claude, etc., and markdown seems like a natural choice for a file format that is both easy for people to write and for AI to understand.


<video src='https://github.com/user-attachments/assets/8038cc92-8833-47c6-a851-09f8fad383c8' width=180></video>


## Contact Information
Having trouble with self-hosting or interested in a managed version with support?
- **Schedule a call**: [https://cal.com/damian.krychowski](https://cal.com/damian.krychowski)
- **Email**: damian.krychowski@hey.com


## License

PlikShare is developed and maintained by Damian Krychowski.
PlikShare is distributed under
an [AGPLv3 license](https://github.com/damian-krychowski/plikshare/blob/main/LICENSE).


## Trademark

The name "PlikShare" and the PlikShare logo are trademarks owned by Damian Krychowski and are not covered by the AGPLv3 license. Please see [TRADEMARK.md](https://github.com/damian-krychowski/plikshare/blob/main/TRADEMARK.md) for usage guidelines.
