# Changelog

Release notes for PlikShare.

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
