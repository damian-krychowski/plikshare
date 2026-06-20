# Changelog

Release notes for PlikShare.

## 1.2.2

- [FIX] Uploading through a public box link no longer redirects the visitor to the sign-in screen — lock-status now uses the box-link token
- [IMPROVEMENT] Box-link and team-box pages go edge-to-edge on mobile — the frame border and side padding are replaced by a single full-width separator
- [FIX] Archive header (ZIP and bulk-upload previews) no longer overflows horizontally on narrow screens — the search box and close button wrap to a new line together

## 1.2.1

- [FEATURE] Agent toolset (MCP) expanded to boxes and members - agents can now manage boxes end to end (list, read, create, update, delete), manage a box's public links (list, create, update, delete, and regenerate a link's access code), and manage both box members and workspace members (list, invite, update permissions, remove). As before, inviting people and destructive actions require approval by default and flow through the human-in-the-loop requests inbox, each with a preview tailored to the box, link settings or members involved
- [FIX] SSO sign-in with Microsoft Entra ID failed with "Email address was not provided by the SSO provider" - Entra often omits the `email` claim (it's only emitted when the user has a `mail` attribute or the optional claim is configured). The callback now falls back to `preferred_username`/`upn` (the UPN, which is an email address for most tenants) when no email claim is returned
- [IMPROVEMENT] Testing an SSO provider's configuration is now optional and no longer blocks creating or saving it - "Test connection" is a separate action that just reports its result, while Create/Save works regardless, so you can finish setup and verify it with a real sign-in. This matters because some providers (Entra in particular) can't be fully confirmed by a pre-save check; a note explains this when a test fails. The Microsoft preset now documents how the email is sourced, and the Password login section was reworked into a config card

## 1.2.0

- [FEATURE] Agents — connect AI agents (e.g. Claude) to PlikShare over MCP. Create an agent, hand it a scoped API token (rotatable, shown only once at creation), and it can work only with the workspaces and boxes you explicitly grant it — its reach is your owned and shared workspaces, nothing more. Per-agent limits: how many workspaces it may create, the default size and team-member caps for those, and which storages it's allowed to use
- [FEATURE] Agent toolset (MCP) — a full set of tools exposed at `/mcp`: list and search workspace content, read files, mint download links (single file and bulk ZIP), create files and folders, rename and move items, create/update/delete public share links, bulk delete, create workspaces, and list workspaces and storages. Agent-skills discovery is built in so a connected agent learns what it can do
- [FEATURE] Human-in-the-loop approval — any tool can be set to require your approval before it runs. Pending operations land in a new Agent requests inbox with a structured preview of exactly what the agent wants to do — which files and folders, share-link settings, the file name/size and where it will be created — and every referenced item links straight into the explorer. Approve or deny each one; the agent polls and commits only once approved. Requests expire after a configurable window and are swept automatically
- [FEATURE] Per-tool permissions on three levels — enable/disable each tool and require-or-not approval per agent globally, then override it per workspace, then per box (the most specific level wins). Destructive tools require approval by default, and creating workspaces is off until you turn it on
- [FEATURE] Agents in boxes — invite an agent to individual boxes the same way you invite people, with its own per-box tool overrides
- [IMPROVEMENT] One-time secret dialogs redesigned — the "agent created" token reveal (and the recovery-code dialog) now use a consistent, cleaner card with a single click-to-copy field

## 1.1.45

- [FIX] Minimap height reworked to size correctly in every embedding context — the app, page-scrolling widgets and fixed-height widget containers. It no longer keeps growing as you scroll, no longer disappears after scrolling back up when there's content above or below the widget, and folders with only a few items no longer get a stray scrollbar. Long lists keep the full-height sticky rail; short lists now show a full-height rail track behind the (short) map, so it looks consistent everywhere
- [IMPROVEMENT] Minimap idle dimming moved from the canvas to a CSS layer — smoother fade and less per-frame canvas work
- [IMPROVEMENT] Widget preview page can simulate page content above and below the widget, to test embedding at different scroll positions

## 1.1.44

- [FIX] Minimap in embedded box widgets — it could collapse to zero height (invisible) when the host page was scrolled, and viewport dragging didn't track the cursor; both fixed

## 1.1.43

- [FEATURE] Audit log size limit — admins can cap the database size; oldest entries are trimmed when exceeded (new installs default to 1 GB, existing stay unlimited), with a "Compact now" action to trim and shrink the file. Admin page split into Configuration and Management sections
- [FIX] Audit log no longer bloats and slows startup — the writer checkpoints its WAL regularly
- [IMPROVEMENT] Minimap — modern look (Inter labels, hairline section headers), full-height rail, and double-click to jump instantly
- [FIX] Minimap ctrl/shift-click selection now works in folders that fit on screen
- [FIX] Minimap no longer adds a stray scrollbar in folders with few items

## 1.1.42

- [FIX] Saving box default display settings failed with 400 — the new gallery layout and tile size values were rejected by the API, so minimap/gallery defaults never reached shared links and widgets
- [FIX] Thumbnails in widgets embedded on external domains could fail with CORS errors — thumbnail responses now send `Vary: Origin` and thumbnail images request in CORS mode, so the browser cache no longer mixes incompatible copies

## 1.1.41

- [FEATURE] Minimap — a VS Code-style miniature of the file explorer (list and gallery views) with a draggable viewport lens; click or scroll it to navigate, hover highlights the real item in both directions, ctrl/shift-click selects straight from the map. Toggled in the display menu, persisted per workspace, smooth even with tens of thousands of files
- [FEATURE] Box default display settings now also cover the minimap, gallery layout and gallery tile size — the card was redesigned into labeled dropdowns
- [IMPROVEMENT] Gallery selection reworked — selected tiles get a passe-partout with a check badge and fade to grey, hover restores full color; mirrored 1:1 on the minimap
- [IMPROVEMENT] Lightbox selection — select/unselect without leaving the lightbox (button or Space/S), with a counter and selection marks in the film strip
- [IMPROVEMENT] Thumbnail loading hardened — prioritized scheduler with watchdog and retries; no more thumbnails stuck loading forever or HTTP/2 connection resets on rapid scrolling
- [FIX] Interrupted thumbnail streams are now aborted properly server-side instead of leaving the browser waiting on a dead request; cancelled transfers no longer flood the server log

## 1.1.40

- [FEATURE] Gallery view — a third file view (next to list and tree), available in workspaces, boxes, widgets and quick shares; selection, search, sorting and bulk actions all keep working, and box owners can set it as the box default
- [FEATURE] Gallery layouts — Justified, Mosaic (with multi-cell hero tiles) and Grid, plus an S/M/L tile-size control; choices persist per workspace and switching animates tiles into the new arrangement
- [FEATURE] Fullscreen lightbox — keyboard/swipe navigation, zoom with pan, slideshow, film strip and permission-aware actions; thumbnails show instantly with neighbours preloaded, and the full-resolution original loads only on demand — saving bandwidth and storage egress. Slow connections get an exact-shape placeholder that morphs between photos
- [FEATURE] Shareable photo links — the open photo is reflected in the URL, and a new single-file endpoint opens such links instantly even in folders with tens of thousands of files
- [IMPROVEMENT] Thumbnail endpoints can serve the Small and Large variants, tiles pick the variant matching their rendered size, and manual generation now produces all three
- [IMPROVEMENT] Image preview and lightbox link to each other — one click in both directions
- [IMPROVEMENT] Large-folder performance — folders with tens of thousands of files navigate, render and switch gallery layouts without lag

[PlikShare gallery](https://github.com/user-attachments/assets/691cefe0-d4e9-4579-b99f-448eef951829)

## 1.1.39

- [FEATURE] Automatic thumbnails — a new opt-in workspace setting generates WebP thumbnails (Mini / Small / Large, your pick) for every uploaded image, and backfills existing images with the sizes they're missing — already existing thumbnails, including manually uploaded ones, are never touched. Live progress in workspace settings; switching off cancels pending generation
- [IMPROVEMENT] Live per-file processing indicators — files currently being processed by background jobs (thumbnail generation, image-dimension extraction) show a spinner in the file list and tree in real time, streamed over a single per-workspace channel that sends only what changed. Backed by a new queue mechanism that tracks exactly which files each job touches, so the indicators stay accurate across reloads and for every user viewing the workspace
- [IMPROVEMENT] Queue jobs now carry their workspace, and batch progress is computed from lightweight counts — bulk operations report progress faster and with less database work
- [IMPROVEMENT] Thumbnails generated in the background are now attributed to the file's uploader — including box visitors — instead of requiring a signed-in user, which is what makes generation on upload possible
- [IMPROVEMENT] UI performance — smoother scrolling in large tree views and cheaper marquee animation of truncated file names
- [FIX] Refreshing the browser on the workspace settings page no longer hides the ffmpeg-dependent sections (image dimensions, thumbnails) — the capability flags are now loaded when the workspace opens, not only when the file explorer does

[Workspace Thumbnails Config](https://github.com/user-attachments/assets/3499a289-651d-4962-9bd5-625d0db46990)

## 1.1.38

- [IMPROVEMENT] Large folders open noticeably faster — listing a folder with thousands of files is 20–30% quicker: thumbnail metadata is fetched in a single query instead of once per file (backed by a new index), and parsed with a lightweight token scanner instead of full JSON deserialization
- [IMPROVEMENT] Background jobs start instantly — the queue wakes up the moment a job is enqueued instead of polling once per second; job pickup also got cheaper (indexed per-category selection instead of ranking the whole backlog) and an idle system no longer takes write locks every second
- [IMPROVEMENT] Smoother responses under heavy background load — database write completions no longer run request code on the single writer thread, so a burst of finished jobs can't stall interactive requests
- [FIX] Thumbnails now follow their file — moving a file to another folder or restoring it from trash left its dependent files (thumbnails, OCR artifacts) assigned to the old folder; deleting that folder could then permanently remove them. Both operations now carry dependent files along, and a migration re-aligns existing data

## 1.1.37

- [IMPROVEMENT] Uploads stay fast in large workspaces — the current size is tracked in memory instead of being recomputed from scratch on every upload
- [IMPROVEMENT] Image-dimension extraction is much faster during big uploads — it no longer re-scans the whole workspace per batch (now indexed)
- [IMPROVEMENT] Lighter background processing — thumbnails, image dimensions and upload finalization commit their result and completion in one transaction instead of two
- [IMPROVEMENT] SQL performance is observable via `dotnet-counters` (`PlikShare.SqliteQueries` / `PlikShare.SqliteWriteQueue`), each query attributed to its call site
- [FIX] Multipart uploads no longer get stuck when the database briefly errors at finalization — the failure was silently swallowed, leaving the file marked "uploading"; it's now retried

## 1.1.36

- [FEATURE] Extract image dimensions on upload — a new opt-in workspace setting reads each uploaded image's pixel dimensions, so previews open at the right size straight away: a placeholder of the exact shape shows instantly and the image drops into it with no layout jump, and stepping to the next/previous image smoothly morphs the frame between differently-shaped photos. Turning the setting on backfills every existing image in the workspace on the background queue — workspace settings show a live progress bar (visible to anyone viewing the page and surviving a reload), a dialog up front tells you how many images will be processed, and switching the setting back off cancels whatever hasn't been processed yet
- [IMPROVEMENT] Quick-share and search previews get the same instant sizing — image dimensions and thumbnail metadata now travel through the quick-share content and the search results, so their image previews show the same instant placeholder and search results display thumbnails
- [IMPROVEMENT] Quick-share file preview gained next / previous navigation and a close (X) button replacing the back arrow, matching the workspace file preview
- [IMPROVEMENT] Clicking items in the box, quick-share and zip file trees now works across the whole row instead of only a narrow central strip
- [FIX] Embedded box-link widget no longer logs an app-capabilities error — that internal-only request is now skipped in contexts that can't manage media (the widget and external pages)

Image Preview:

[Image preview](https://github.com/user-attachments/assets/874c2f50-b622-4123-aaeb-d465da34a485)

Workspace new config section:

[Workspace new config](https://github.com/user-attachments/assets/52a7f46c-2d00-4ccb-bab0-8af1b276b7d1)

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

### 1.0.22
- **[FEATURE]** Added AWS Textract integration - enabling text extraction from PDF/JPEG/etc with OCR engine


### 1.0.21
- **[IMPROVEMENT]** File preview can open files which are not recognized and display their content as text
- **[IMPROVEMENT]** Bulk zip upload for S3 storages optimized in terms of memory usage

### 1.0.20
- **[BUG FIX]** PDF files preview opens the file instead of downloading it

### 1.0.19
- **[FEATURE]** Bulk zip upload - initial version
- **[IMPROVEMENT]** Performance improvements on most major features (uploads, deletes, folders creation, file previews etc)
- **[BUG FIX]** Decryption of files in some special edge-cases was failing.

### 1.0.18
- **[IMPROVEMENT]** Zip preview supports Zip64 format (zip files larger than 4GB* etc.)

### 1.0.17
- **[IMPROVEMENT]** Application search now enables file opening and preview redirection
- **[BUG FIX]** Fixed UI issues for box content preview
- **[BUG FIX]** Fixed UI issues in box folder picker dialog
- **[BUG FIX]** Search item highlight animation now disappears after 5 seconds (previously remained indefinitely)

### 1.0.16
- **[IMPROVEMENT]** ZIP archive preview is now available within boxes.
- **[IMPROVEMENT]** ZIP archive preview now allows users to search for content within archives.

### 1.0.15
- **[FEATURE]** File preview now displays contents of ZIP archives and enables direct downloads of individual files within the archive.

### 1.0.14
- **[IMPROVEMENT]** File preview is now available for all file types - including those without preview capability - allowing users to add notes and comments to any file.
- **[IMPROVEMENT]** Email picker now suggests existing users when inviting new members to workspaces and boxes.

### 1.0.13
- **[FEATURE]** File preview now supports adding notes and comments.
- **[IMPROVEMENT]** Files opened in preview are reflected in the URL and can be accessed directly via link.

### 1.0.12
- **[IMPROVEMENT]** File preview UI/UX improved. More files types supported: video, audio, pfds, images and text files.
- **[BUG FIX]** File lock checking happens only when needed

### 1.0.11
- **[IMPROVEMENT]** Storage creation view improved (separate pages instead of dialog boxes)
- **[IMPROVEMENT]** Better memory efficiency in range download requests for encrypted file storages

### 1.0.10
- **[IMPROVEMENT]** Video preview works on encrypted storages

### 1.0.9
- **[IMPROVEMENT]** S3 multipart upload mode is used only for large enough files

### 1.0.8
- **[BUG FIX]** Resolved length limitation issue affecting internal pre-signed links used for bulk downloads and hard-drive storage operations
- **[IMPROVEMENT]** Users with upload-only permissions can now bulk-delete their own files from storage boxes
- **[IMPROVEMENT]** Refined UI elements and enhanced encryption option descriptions for S3 integration

### 1.0.7
- **[FEATURE]** Added managed encryption to storages
- **[IMPROVEMENT]** File Explorer displays counts of folders, files and uploads, allowing selection by clicking
- **[IMPROVEMENT]** File Explorer supports CTRL+A for selecting all items and includes an additional checkbox in the top bar for this function
- **[IMPROVEMENT]** More efficient uploads based on file sizes (app determines upload method based on criteria: complete file or chunk-by-chunk)
- **[BUG FIX]** Recently uploaded files remain locked until upload is complete to prevent potential issues

### 1.0.6
- **[FEATURE]** Bulk download now supports both files and folders while preserving folder structure in the output zip
- **[BUG FIX]** Legal documents (Terms of Service and Privacy Policy) now display correctly after being uploaded in General Settings

### 1.0.5
- **[FEATURE]** Added initial version of bulk files download
- **[BUG FIX]** When user enables or disables multi-factor authentication he no longer gets logged out

### 1.0.4
- **[FEATURE]** Added image and video preview functionality (click on image/video files to open preview dialog)
- **[IMPROVEMENT]** File extensions now displayed in File Explorer item details

### 1.0.3
- **[IMPROVEMENT]** User who created a folder in a box has 5min to change its name when "rename folder" permission is not granted.

### 1.0.2
- **[BUG FIX]** Box header and footer should not be displayed if they are disabled even though they have some content.
- **[BUG FIX]** Wrong email template is sent when user accepts workspace invitation or when user leaves shared workspace.
- **[BUG FIX]** After accepting workspace invitation, new shared workspace size is always displayed as 0.
- **[BUG FIX]** Email provider 'Send From Email' field is no longer converted to lower case letters only.
- **[IMPROVEMENT]** Box details page performance improvements.


### 1.0.1
- **[BUG FIX]** Fixed handling of folder selection for a box in box list page when box's previously assigned folder has been deleted 


### 1.0.0
- Initial release 
