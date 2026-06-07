# Changelog

Release notes for PlikShare.

## 1.1.36

- [FEATURE] Extract image dimensions on upload — a new opt-in workspace setting reads each uploaded image's pixel dimensions, so previews open at the right size straight away: a placeholder of the exact shape shows instantly and the image drops into it with no layout jump, and stepping to the next/previous image smoothly morphs the frame between differently-shaped photos. Turning the setting on backfills every existing image in the workspace on the background queue — workspace settings show a live progress bar (visible to anyone viewing the page and surviving a reload), a dialog up front tells you how many images will be processed, and switching the setting back off cancels whatever hasn't been processed yet
- [IMPROVEMENT] Quick-share and search previews get the same instant sizing — image dimensions and thumbnail metadata now travel through the quick-share content and the search results, so their image previews show the same instant placeholder and search results display thumbnails
- [IMPROVEMENT] Quick-share file preview gained next / previous navigation and a close (X) button replacing the back arrow, matching the workspace file preview
- [IMPROVEMENT] Clicking items in the box, quick-share and zip file trees now works across the whole row instead of only a narrow central strip
- [FIX] Embedded box-link widget no longer logs an app-capabilities error — that internal-only request is now skipped in contexts that can't manage media (the widget and external pages)

## 1.1.35

- [FEATURE] Thumbnails in the folder tree — preview thumbnails now show inline in the tree view, not just the list view
- [FEATURE] Generate thumbnails from a tree selection — the "Generate thumbnails" action now operates on the files and folders selected in the tree (with include/exclude just like the other bulk actions), and a count of how many files will be processed is shown before generation starts
- [IMPROVEMENT] Live thumbnail-generation progress in the tree view — a progress bar plus per-file spinners light up as each thumbnail is generated, including during a bulk re-generation over many files
- [FIX] Search in full-encryption workspaces — searching files and folders returned nothing because the encrypted names were never matched against the query; names are now decrypted server-side for matching and returned in plain text, and a matching file's thumbnail shows up in the results

[Thumbnails in tree view](https://github.com/user-attachments/assets/66a0c8bb-cdb5-413f-9b79-dcbccdbe4518)

## 1.1.34

- [FEATURE] Per-box default display settings — each box now has a "Default display settings" card where the owner sets how the box looks when opened: initial view (list or tree), initial sorting (custom order, or name ascending/descending), and whether thumbnails start on. These defaults apply every time the box is opened through a shared link, an embedded widget, or the box preview
- [IMPROVEMENT] Config cards can be collapsed — cards opt into a chevron button that folds the card down to just its title and description, keeping long settings pages tidy
- [FIX] Box preview now respects the box's default sort — when opening a box you own (full permissions), files and folders showed in their raw order even though the sort indicator matched the configured default, because the list captured its order before the content finished loading; the configured sort is now applied correctly

## 1.1.33

- [FIX] Embedded box-link widget — the widget stopped working after the last release; the custom element failed to register because the embedded mini-app no longer provided the router that the file explorer depends on. Widgets embedded on external pages work again
- [IMPROVEMENT] Widget adapts to its container — when embedded in a fixed-height container the file list now scrolls internally with the header staying put, instead of overflowing and getting clipped; when given no fixed height it keeps growing with the page as before. The scrollbar is slim and the sticky header automatically blends into the background colour of the page it's placed on
- [FEATURE] Live widget preview — a preview page (opened from the box widget setup) embeds the actual widget so you can see how it looks and behaves before installing it on your site
- [IMPROVEMENT] Disabled-box screen redesigned — the box-link widget, external box and external link pages now show a clean centered empty state (icon, title, short note) instead of the previous red warning banner

## 1.1.32

- [FEATURE] Thumbnails for images and videos — preview thumbnails (Mini / Small / Large) are generated server-side via ffmpeg for image and video files; generation runs on the background queue with live per-batch progress pushed over SSE, can be triggered for a single file or in bulk, and the thumbnails show up inline in the file-explorer list view as well as in box-link and quick-share previews
- [FEATURE] Download images as a different type — image files can be downloaded converted on the fly to JPEG, PNG or WebP
- [IMPROVEMENT] In-browser image preview reworked for a smoother, higher-quality view
- [IMPROVEMENT] Large-folder performance — the file explorer, the folder tree, the box/quick-share file tree and the trash list are now virtualized and load lazily, so workspaces with thousands of items scroll smoothly instead of stuttering
- [IMPROVEMENT] Long file names that don't fit scroll horizontally (marquee) on hover/selection instead of being silently truncated
- [IMPROVEMENT] Docker image now ships in two variants — the default `latest` stays slim, and a `latest-ffmpeg` (and `<version>-ffmpeg`) variant bundles a static ffmpeg so thumbnail generation works out of the box; the slim image runs fine without it (thumbnail generation simply stays off until an ffmpeg binary is available)
- [FIX] Bulk-uploading large compressed zip archives no longer crashes Chrome with an out-of-memory error

## 1.1.31

- [FEATURE] Quick-share file preview — recipients can preview individual files in the browser and browse nested zip archives before downloading, instead of having to grab the bundle blindly
- [IMPROVEMENT] Quick-share details page reworked — all sections (URL slug, recipient mode, expiration, download limit, password) rebuilt as titled config cards with descriptions and inline header actions; expiration picker blocks past dates client-side; password-change dialog tells the user what's actually happening (add vs change) instead of an inaccurate "cannot be reverted"; downloads count moved into the metadata block
- [IMPROVEMENT] External quick-share page redesigned — sticky owner-preview banner, centered password gate with lock icon and inline validation under the field, friendlier copy, footer pinned to the bottom on short content; confirm buttons across the app (invitation links, recovery code, master-password change/reset, folder picker, restore from trash) dropped the blue primary look in favour of the bordered style used everywhere else
- [FIX] Workspace delete with quick shares — deleting a workspace that had any quick shares failed silently (foreign-key rollback) and left the workspace in place; quick shares are now cleaned up first
- [FIX] External quick-share unlock — wrong password no longer redirects the visitor to the app's sign-in page; the error surfaces inline under the input

**Quick share details view:**
<img width="2038" height="2434" alt="plikshare_quick_share_details" src="https://github.com/user-attachments/assets/16d19a3b-734a-4863-b9ee-44b9a8845749" />

**Quick share access with password:**
<img width="2038" height="1104" alt="pliskshare_quick_share_password" src="https://github.com/user-attachments/assets/f73c61f6-3dff-4826-89b8-e1e9c0b361d1" />

[Quick share file preview](https://github.com/user-attachments/assets/2d53f264-cc59-42ce-b189-ef9f003287e2)

## 1.1.30

- [FEATURE] Trash introduced — with a workspace's trash policy enabled, deleted files are moved to a per-workspace trash instead of being removed right away; from the Trash view they can be restored to their original location or to a folder of choice, permanently deleted one by one, or cleared all at once
- [FEATURE] Configurable retention window — trashed files are automatically purged after a set number of days, or kept indefinitely; a background sweeper enforces retention and records each automatic purge in the audit log under a system actor
- [FEATURE] Storage-level default trash policy — admins set a default trash policy on each storage; new workspaces inherit it (snapshotted at creation time) and can override it afterwards
- [IMPROVEMENT] Workspace configuration now presents every option — size, team members, trash, audit log — as a consistent titled card with a short description
- [IMPROVEMENT] User details settings (permissions, workspace limits, default workspace config, storage access) reworked into the same titled-card layout, replacing the previous inline labels and headers
- [IMPROVEMENT] General settings — the new-user defaults section adopts the same titled-card layout for permissions, workspace count, default size, default team members and storage access
- [IMPROVEMENT] Storage creation forms group the encryption type and default trash policy under an "Options" section, each presented as the same titled card used elsewhere
- [IMPROVEMENT] Box folder picker reworked — a destination bar above the actions shows which folder will be shared and an explicit "Share this folder" button confirms the choice, instead of a per-folder share button; the dialog grows to fit the explorer instead of scrolling
- [IMPROVEMENT] Admin first-run setup screen redesigned as a centered card — matching the empty-dashboard look — with numbered steps; the email step is marked optional since users can also be invited by link

[Storage default trash policy](https://github.com/user-attachments/assets/4bff2031-b9ee-4b86-a311-d4afb6352a11)

[Workspace trash policy update](https://github.com/user-attachments/assets/f51f4f9e-9856-4842-834a-d40cca9cb38d)

[Delete/Restore from trash](https://github.com/user-attachments/assets/c094c6f8-111f-4b52-9890-9ecddacfea84)

## 1.1.29

- [IMPROVEMENT] Multi-item selection for downloading from zip previews and quick shares
- [IMPROVEMENT] Search and select-all in the quick share view

[Quick share bulk download](https://github.com/user-attachments/assets/be5cd9d1-abcd-40da-b75f-32c9ceb5583f)

[Zip bulk download](https://github.com/user-attachments/assets/20b196ba-d2a0-4406-b8ae-efda0e1b5aab)

## 1.1.28

- [FEATURE] Quick shares introduced — share selected files and folders via a link with optional custom URL slug, expiration, password, and download cap; recipient gets either a browseable preview (file tree + per-file preview + optional ZIP bundle) or a direct ZIP download
- [IMPROVEMENT] Files-explorer mobile layout — toolbar splits into a path row and an actions row content-driven by available width (ResizeObserver-based, no fixed breakpoint), search collapses behind a magnifier icon
- [IMPROVEMENT] Workspace-manager top bar collapses in three stages as the viewport shrinks (full → icon-only → hamburger menu) instead of a single mobile cliff
- [IMPROVEMENT] Decluttered file and folder rows — per-item download, share, delete and "Edit name" buttons removed; bulk actions live in the toolbar after selection, and tapping the name starts editing on mobile
- [IMPROVEMENT] Unified view mode and sort order into a single "List · Custom ▾" display menu in the context bar
- [IMPROVEMENT] Folder / file / upload counts in the context bar shown as icon chips with tooltips instead of text

[Quick share](https://github.com/user-attachments/assets/896b0c39-1857-4f88-acd4-750576d63f59)

## 1.1.27

- [FEATURE] Configurable audit-log policy — admins can disable individual events and override their severity (verbose/info/warning/critical) at the application level, set defaults for new workspaces, and tune the policy of each workspace individually
- [FEATURE] Event volume stats (events / 30d) shown next to every entry in the policy editor so admins can spot noisy events worth disabling

[Audit logs policy](https://github.com/user-attachments/assets/6d60ec67-cdbe-4f96-bc0a-ddfdbac7ee00)

## 1.1.26

- [FEATURE] Admin can invite users via a shareable link instead of email — useful when no email provider is configured, or when the admin prefers to deliver the invitation out-of-band
- [SECURITY] Plaintext invitation code for full-encryption workspace invites no longer persists in queue job payloads — it doubles as the KEK for the invitee's ephemeral private-key wrap. Email is now sent synchronously after DB commit; on failure the staged rows are rolled back. Non-FE flows unchanged.

[Inviate via link](https://github.com/user-attachments/assets/849a7945-9a3e-40e8-b8bd-f435a7fd7774)

## 1.1.25

- [FEATURE] Per-user storage access policy introduced — admins can restrict which storages a user is allowed to create workspaces on
- [FEATURE] Default storage access policy for newly invited users configurable from general settings (snapshotted onto each user at invitation time)
- [IMPROVEMENT] Full-encryption storages are shown as disabled in the storage-access UI — sharing them with other users is not supported yet

[Storage access management](https://github.com/user-attachments/assets/1a0cd4bc-daf0-4cf3-ae18-bcc3324f31f5)

## 1.1.24

- [FEATURE] Multi-item drag-and-drop introduced
- [FEATURE] Admin can assign a user as a member of an existing workspace from the user-details view
- [FEATURE] Admin can transfer ownership of an existing workspace to a user from the user-details view

Multiple items drag&drop:

[Drag and drop of multiple items](https://github.com/user-attachments/assets/6fe1c5af-ce1f-4e59-9aea-ecb953805119)

User settings details: 

[Assign user to workspace as member/owner](https://github.com/user-attachments/assets/0db088e7-1580-46ce-9de6-fb2669d10b36)

## 1.1.23

- [FEATURE] Drag-and-drop reordering for files and folders introduced
- [IMPROVEMENT] Drill-down into folders while dragging an item
- [FEATURE] Items sorting by name introduced
- [FEATURE] Items sorting by date introduced

[Drag and drop for files and folders](https://github.com/user-attachments/assets/08035dd2-02f7-4790-8d00-1eb78d24a8cb)

---

> Versions prior to `1.1.23` are not documented here — this changelog was introduced mid-stream. For older history see the git log.
