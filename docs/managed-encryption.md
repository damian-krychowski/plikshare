# Managed-Encryption in Plikshare

A technical description of managed encryption mode in Plikshare: the server-held master password, the per-storage IKM hierarchy, the file frame format, and the recovery mechanism.

## Table of contents

- [Design goals](#design-goals)
- [Server master password](#server-master-password)
- [Storage IKM hierarchy](#storage-ikm-hierarchy)
- [Storage Creation Flow](#storage-creation-flow)
- [Storage Recovery codes](#storage-recovery-codes)
- [File Encryption Flow](#file-encryption-flow)
- [What is NOT encrypted](#what-is-not-encrypted)
- [Storage IKM rotation](#storage-ikm-rotation)
- [Choosing between managed and full encryption](#choosing-between-managed-and-full-encryption)

## Design goals

Managed-encryption mode is built around two properties:

1. **Resilient to file-storage compromise.** An attacker who obtains the file storage alone — an exposed S3 bucket, a stolen disk volume, a leaked backup of the blob layer — cannot read file contents. Storage holds only encrypted file frames; the keys that decrypt them live in the database, themselves wrapped under the server's master password.

2. **Recoverable from a recovery code alone.** If the database is lost, the storage owner holding a recovery code can still decrypt files left in storage. The recovery code is the disaster-recovery root — the same input that produced the original IKMs at creation time produces them again, deterministically, with no DB row, no admin assistance, no backup of any wrapped key required.

The server is in the trust boundary. Day-to-day decryption happens server-side without any user action — when a user requests a file, the server reads the storage's encrypted IKM blob from the database, unwraps it with the master password held in process memory, derives the file's AES key, and streams plaintext back. This is the trade managed encryption makes: convenience and zero per-user setup, paid for with full server access to every file at every moment.

### Out of scope

An attacker with **simultaneous access to the database and the server's master password** is out of scope. The IKMs stored in `s_encryption_details_encrypted` are wrapped under a key derived from that password; once both are in hand, every file in every managed storage is reachable. This is the threat model managed encryption explicitly does not defend against — it is what [full encryption](full-encryption.md) is for.

An attacker with **live access to the server process**, capable of dumping process memory while the master password is resident, is also out of scope. The master password lives in pinned + mlocked `SecureBytes` for the entire process lifetime by design — every PBKDF2 derivation that wraps a storage row needs the password bytes available. Mitigation focuses on the password never leaking outside the process: it is held off the GC heap, never serialized, never logged.

## Server master password

The server's master password is provided as the `EncryptionPasswords` configuration value, read once at startup from environment / app settings. The Docker / install scripts expose it as the `PlikShare_EncryptionPasswords` environment variable. After startup the password lives only inside `MasterEncryptionKeyProvider` as a pinned + mlocked `SecureBytes` — never as a plain `string` on the GC heap.

```
┌──────────────────────────┐
│  PlikShare_Encryption-   │
│  Passwords               │
│  (env var, set at        │
│   deployment time)       │
└────────────┬─────────────┘
             │
             │  comma-split → list of master passwords
             │  (id 1 = oldest, last = current)
             ▼
┌──────────────────────────┐
│  MasterEncryptionKey     │
│  per id                  │
│  (raw password held as   │
│   SecureBytes —          │
│   pinned + mlocked,      │
│   process-lifetime)      │
└──────────────────────────┘
```

The configuration value can carry **multiple passwords** separated by commas. The last one in the list is the current encryption key; older entries (id 1, id 2, …) exist only to decrypt rows written by previous deployments. New writes always use the latest. This is the rotation handle for the master password itself — operators can introduce a new password at the end of the list, redeploy, and over time re-encrypt rows under the new id; the older password stays in the list until no rows reference its id any more.

### Wrapping a database row

The master password does not encrypt anything directly. Each row that needs encryption gets its own AES-GCM key derived from the password through PBKDF2:

```
┌──────────────────────────┐
│  MasterKey.PasswordBytes │
│  (SecureBytes)           │
└────────────┬─────────────┘
             │
             │  PBKDF2-SHA256
             │  salt = freshly random per row (16 bytes)
             │  iterations = 650 000
             ▼
┌──────────────────────────┐
│  Per-row AES-GCM key     │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  AES-GCM, fresh nonce per encrypted value
             ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│  Frame:                                                                                 │
│  MasterKeyId(1) | IterationsFactor(2) | Salt(16) | Nonce(12) | Tag(16) | Ciphertext(N)  │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

The salt is embedded in the frame, so the read path can re-derive the same AES key from the same password without consulting any external state. Two rows under the same master password produce unrelated ciphertexts because their salts differ. `IterationsFactor` records the iteration count used at write time, so a future deployment can raise the cost factor without breaking older rows.

A single PBKDF2 derivation is amortized across both encrypted columns of a storage row: `s_details_encrypted` (the storage's configuration JSON — bucket name, S3 keys, hard-drive path) and `s_encryption_details_encrypted` (the IKM list) share the same derived key on the same row, distinguished only by their AES-GCM nonces. From the wire format's perspective each column is a self-contained frame with its own salt; in practice they were produced from one PBKDF2 call.

What this means for trust: anyone with read access to the deployment's environment variables (a leaked `.env`, a misconfigured CI log, a compromised Docker host) holds the master password. Combined with a database dump, every IKM blob can be re-derived (PBKDF2 with the per-row salt from the frame), and every managed-encryption file becomes readable. Treat `PlikShare_EncryptionPasswords` as the most sensitive secret in the installation — losing it together with the database is equivalent to losing every managed-encryption file.

## Storage IKM hierarchy

The encryption mechanism for a managed storage is built around a list of **input key materials** (IKMs). Each IKM is a 32-byte secret; one of them is selected per file at write time and recorded in the file's header. Files written under different IKMs coexist without ambiguity, and the version byte in each header tells the reader which IKM to fetch.

```
            ┌──────────────────────────┐
            │  Recovery seed           │
            │  [32 random bytes,       │
            │   ephemeral, returned    │
            │   once as a 24-word      │
            │   BIP-39 mnemonic]       │
            └────────────┬─────────────┘
                         │
                         │  HKDF-SHA256
                         │  info = ("plikshare-dek\0" || version_be32)
                         ▼
            ┌──────────────────────────┐
            │  IKM v{version}          │
            │  [32 bytes]              │
            │  (persisted, base64,     │
            │   inside an encrypted    │
            │   blob keyed to the      │
            │   server master         │
            │   password)             │
            └────────────┬─────────────┘
                         │
                         │  HKDF-SHA256, salt = FILE_SALT
                         ▼
            ┌──────────────────────────┐
            │  File AES key            │
            │  [32 bytes, ephemeral]   │
            └──────────────────────────┘
```

The list of IKMs is stored on the storage row in `s_encryption_details_encrypted`, as a JSON document — `{"Ikms": ["base64...", "base64..."]}` — wrapped under the per-row scheme described in [Wrapping a database row](#wrapping-a-database-row). Two storages in the same database, even with the same master password, have unrelated ciphertexts.

What survives across a key-derivation step is the property the parent already had: the recovery seed alone is enough to re-derive every IKM under it (`DeriveDek(seed, v)` is deterministic), and an IKM plus a per-file `FILE_SALT` is enough to derive that one file's AES key. Holding the recovery seed reaches every file in the storage; holding only one file's IKM reaches every file in the storage too — but only because the IKM is shared across files, not derived per-file.

### IKMs in memory

The PBKDF2 derivation that unwraps an IKM blob is paid only once per storage. At application startup, every row in `s_storages` is read, its `s_encryption_details_encrypted` is unwrapped (one PBKDF2 per row, against the salt stored in the frame), and the resulting IKMs are decoded into a `version → byte[]` map held inside the storage's `ManagedStorageEncryption` instance. From that point on, encryption and decryption of files served by that storage read the IKM directly from the in-memory map — no further PBKDF2, no further AES-GCM unwraps of the storage row.

The cost model that follows is straightforward: startup time grows linearly with the number of managed-encryption storages (650 000 PBKDF2 iterations per storage), file operations on a running server are unaffected by the master-password derivation entirely.

## Storage Creation Flow

Creating a managed-encryption storage is the moment the first IKM comes into existence. A single random recovery seed is generated, and from it everything else follows: IKM v0, the persisted IKM blob, the recovery code returned to the creator.

```
┌──────────────────────────┐
│  Recovery seed           │
│  [32 random bytes,       │
│   ephemeral]             │
└────────────┬─────────────┘
             │
             ├───────────────────────────────┐
             │                               │                             
             │  HKDF-SHA256,                 │  RecoveryCodeCodec           
             │  info = ("plikshare-dek\0"    │  (BIP-39, 24 words)          
             │       || version_be32(0))     │                              
             ▼                               ▼                              
┌──────────────────────────┐   ┌──────────────────────────┐
│  IKM v0                  │   │  Recovery code           │
│  [32 bytes,              │   │  (returned to creator    │
│   base64-encoded]        │   │   exactly once)          │
└────────────┬─────────────┘   └──────────────────────────┘
             │
             │  Json.Serialize({"Ikms": ["<b64>"]})
             │  → AesGcmMasterDataEncryption.Encrypt
             │     (AES-GCM under PBKDF2(master pwd, row salt))
             ▼
┌──────────────────────────────┐
│  s_encryption_details_       │
│  encrypted                   │
│  (one BLOB per storage row)  │
└──────────────────────────────┘
```

The seed feeds two independent derivations and is then zeroed. It is never persisted. What survives in the database is the encrypted IKM blob (so normal operation never needs to touch the seed). The seed itself leaves the server only as the 24-word mnemonic returned in the response.

The recovery code is returned in the response exactly once. There is no endpoint to fetch it later, no copy held server-side, no row to recover it from. From the moment the response is flushed, the only copies of the recovery seed in existence are the ones the creator wrote down.

A creator does not need any per-user encryption material to make a managed-encryption storage. There is no public key to seal to, no encryption password to set, no session to unlock. The flow is symmetric in the user dimension — every user with permission to create a storage can create a managed one, regardless of whether they have configured an encryption password (which is a full-encryption concept).

## Storage Recovery codes

The recovery code returned at storage creation is the disaster-recovery root for the entire storage. It is not used during normal operation — day-to-day decryption goes through `s_encryption_details_encrypted`, where the IKM list is already wrapped under the master password and unwrapped on demand. The recovery code matters in two scenarios:

- The database is gone, and with it every encrypted IKM blob.
- The master password is gone (the env var was rotated and no operator kept a copy of the previous value, or the deployment was migrated and the password was lost in transit), making the IKM blobs unreadable.

In either scenario the file storage may still be intact — S3 buckets, local volumes, whatever the backend was — full of encrypted file frames that nobody can read. Every V1 file header carries the IKM version, the per-file salt, and the per-file nonce prefix, so the only missing input is the IKM itself. The recovery code is exactly that input.

```
┌──────────────────────────┐
│  24-word BIP-39 mnemonic │
│  (held by the creator)   │
└────────────┬─────────────┘
             │
             │  RecoveryCodeCodec.TryDecode
             ▼
┌──────────────────────────┐
│  Recovery seed           │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  HKDF-SHA256,
             │  info = ("plikshare-dek\0"
             │       || version_be32(v))
             ▼
┌──────────────────────────┐
│  IKM v{v}                │
└────────────┬─────────────┘
             │
             │  for each file: read header,
             │  HKDF(IKM, FILE_SALT) → file AES key
             ▼
┌──────────────────────────┐
│  Per-file AES key        │
│  → decrypt frame         │
└──────────────────────────┘
```

The same derivation that produced IKM v0 at creation time produces it again from the same seed. A file plus the recovery seed is enough to derive its AES key without consulting any external state.

This recovery path is not yet exposed in the product — no endpoint accepts a recovery code, no UI prompts for it. The intended consumer is an out-of-band tool that operates directly on the file storage: point it at a bucket, give it the 24-word mnemonic, and let it walk every encrypted frame, deriving keys from headers and emitting plaintext. No database, no running server, no master password — only the storage and the seed.

What the recovery code cannot reconstruct is anything that lived only in the database — workspace structure, file names and folder paths, audit history, share links, user accounts. Recovery is a path back to the bytes, not back to the application state.

## File Encryption Flow

Every file written to a managed-encryption storage is encrypted before any byte leaves the server. The handler resolves the storage's `ManagedStorageEncryption` (which has the IKM list pre-decoded into a `version → ikm` map), picks the latest IKM version, generates a fresh per-file salt and nonce prefix, derives the file's AES key, and streams ciphertext to the storage backend.

### Runtime path

Encrypting a new file:

```
┌──────────────────────────┐
│  ManagedStorageEncryption│
│  .GetEncryptionKey(      │
│    LatestKeyVersion)     │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  IKM (latest version)    │
│  [32 bytes]              │
└────────────┬─────────────┘
             │
             │  HKDF-SHA256,
             │  salt = FILE_SALT
             │  (freshly random, 32 bytes)
             ▼
┌──────────────────────────┐
│  File AES key            │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  AES-256-GCM,
             │  IV = NONCE_PREFIX || segment_no || flag
             ▼
┌──────────────────────────┐
│  Segment ciphertext+tag  │
│  (1 MiB plaintext per    │
│   segment, streamed)     │
└──────────────────────────┘
```

The file's header is written into segment 0 of the storage object: format byte (the size constant), the IKM version, the freshly random `SALT`, and the freshly random `NONCE_PREFIX`. The same fields are also persisted on the file's row in the database. The header carries everything a future reader needs to find the right key — whether that reader is the live application or an offline recovery tool.

The duplication into the database row is what makes range reads cheap: to decrypt a segment from the middle of a large file, the server reads `KEY_VERSION`, `SALT` and `NONCE_PREFIX` straight from the row, without first issuing a storage round-trip just to fetch the header bytes. The in-file copy still matters — it is what lets an offline recovery tool decrypt a file with no database at all — but on the hot path, the database row is the source of truth.

Decrypting an existing file:

```
┌──────────────────────────┐
│  File header             │
│  (read from storage)     │
└────────────┬─────────────┘
             │
             │  parse KEY_VERSION,
             │  SALT, NONCE_PREFIX
             ▼
┌──────────────────────────┐
│  ManagedStorageEncryption│
│  .GetEncryptionKey(      │
│    KEY_VERSION)          │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  IKM (matching version)  │
└────────────┬─────────────┘
             │
             │  HKDF-SHA256,
             │  salt = SALT
             ▼
┌──────────────────────────┐
│  File AES key            │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  AES-256-GCM verify+decrypt
             │  per segment
             ▼
┌──────────────────────────┐
│  Plaintext stream        │
└──────────────────────────┘
```

`SALT` is freshly random per file. Two files in the same storage share the same IKM but have independent AES keys, and the same plaintext written twice produces two unrelated ciphertexts.

### File frame format

Files use a streaming AEAD adapted from [Google Tink](https://developers.google.com/tink/streaming-aead). Managed-encryption storages use the **V1** frame format throughout — the header is fixed-size and assumes a single server-managed key context, which is exactly what managed encryption provides.

### Why streaming

Three reasons: large files, HTTP `Range:` requests, and parallel uploads. A single AES-GCM encryption forces the entire plaintext through memory and provides no random access. Streaming AEAD splits the file into segments, each independently authenticated. Any segment can be decrypted without touching the others. The same property works in reverse on upload: parts can be encrypted and pushed to storage in parallel, which lines up cleanly with S3-compatible multi-part upload APIs offered by most providers.

```
Segment size:           1 MiB (1,048,576 bytes)
Segments per file part: 10
Max payload per part:   10 MiB
```

10 MiB file parts align with the multi-part upload split.

### V1 frame layout

```
First segment:
┌──────────────────────────────────────────────────────────────────────┐
│ HEADER (41 bytes) │           CIPHERTEXT            │   TAG (16 B)   │
└──────────────────────────────────────────────────────────────────────┘

Header = SIZE(1) | KEY_VERSION(1) | SALT(32) | NONCE_PREFIX(7)

Subsequent segments:
┌──────────────────────────────────────────────────────────────────────┐
│                       CIPHERTEXT                    │   TAG (16 B)   │
└──────────────────────────────────────────────────────────────────────┘
```

41 bytes of header in segment 0, no header anywhere else. Total cipher overhead: 41 + 16 × `segment_count` bytes.

Each segment's IV is constructed deterministically from the file-level `NONCE_PREFIX`, the segment's index, and a final byte that distinguishes the last segment from the rest:

```
IV = NONCE_PREFIX (7 bytes) | segment_no (4-byte big-endian) | flag (1 byte)

flag = 0x00  for non-final segments
flag = 0x01  for the final segment
```

The last-segment flag is what stops truncation attacks: an attacker cannot drop trailing segments and have the result re-authenticate, because the final segment's IV is shaped differently from any intermediate one.

### Range reads on encrypted blobs

HTTP `Range:` is required for a file-sharing application — video streaming, resumable downloads, partial reads. Encrypted blobs change the math: a plaintext byte range maps to an encrypted byte range that includes the full segment containing each endpoint.

```
Plaintext range:                        [P_start ─────── P_end]
                                                    │
                                                    ▼
Encrypted range: [seg_k CIPHERTEXT][TAG][seg_k+1 CIPHERTEXT][TAG] ... [seg_n CIPHERTEXT][TAG]
                  │                                                    │
                  └── start segment                                    └── end segment
```

The header bytes do not need to be fetched on the hot path: `KEY_VERSION`, `SALT` and `NONCE_PREFIX` are already on the file's database row. Given those fields and the requested plaintext range, the server computes which segments cover the range, fetches just those segment ranges from storage, authenticates and decrypts each independently, and yields plaintext into the response stream with the trim offsets applied.

## What is NOT encrypted

Managed encryption protects file **contents**. Everything else stays in the database in plaintext:

- **File names and folder names** — stored as-is on `fi_files` / `fo_folders` rows.
- **Workspace and box names** — stored as-is.
- **Comment bodies and link names** — stored as-is.
- **Audit log details** — file paths, renames, invitation emails are recorded verbatim.
- **Storage configuration** — bucket names, S3 endpoints, hard-drive paths.

The reason is consistent with the threat model: the database is already inside the trust boundary (the server reads it constantly to operate), and the master password is already on the server. Encrypting metadata under that same password would produce a cosmetic layer that any attacker who already holds the password can lift. The work is only worthwhile when there is a key the server itself does not hold — which is exactly the construction full encryption introduces.

If file/folder names matter to the threat model, [full encryption](full-encryption.md) is the mode that covers them. The two modes can coexist in the same installation: managed-encryption storages and full-encryption storages live side by side, and the choice is made per storage at creation time.

## Storage IKM rotation

The format and key list were designed for rotation, but rotation itself is not yet implemented. There is currently exactly one IKM version per managed storage — version 0 — and no mechanism to introduce a new one.

What the format already supports:

- The V1 file header carries `KEY_VERSION`, so files written under different versions coexist without ambiguity.
- `ManagedStorageEncryption` indexes its IKMs by version and exposes `GetEncryptionKey(version)`, so decryption picks the right key per file.
- `StorageDekDerivation.DeriveDek(seed, v)` is parameterized by version, so a single recovery seed re-derives every past IKM on demand.

What is missing is the operation itself: the act of generating a new IKM, appending it to the storage's IKM list, re-encrypting `s_encryption_details_encrypted`, and switching new writes over to the new version. When rotation is added, existing files keep their version byte and remain decryptable under the old IKM; new files will be written under the new IKM, and the read path will continue to dispatch by version with no further changes.

## Choosing between managed and full encryption

| Property                           | No encryption | Managed                | Full                          |
|------------------------------------|---------------|------------------------|-------------------------------|
| File contents at rest in storage   | plaintext     | encrypted              | encrypted                     |
| File / folder names in DB          | plaintext     | plaintext              | encrypted                     |
| Audit log details                  | plaintext     | plaintext              | encrypted (sensitive fields)  |
| Survives file-storage compromise   | no            | yes                    | yes                           |
| Survives DB compromise             | n/a           | yes (without master pw)| yes                           |
| Survives DB + master-password leak | n/a           | **no**                 | yes                           |
| Per-user encryption setup          | none          | none                   | required (encryption password)|
| Server can read files at any time  | yes           | yes                    | no                            |
| Recovery from a 24-word code       | n/a           | yes (per storage)      | yes (per storage and per user)|

Managed encryption is the right choice when the threat being defended against is **storage-only compromise** — a leaked S3 backup, an exposed disk volume, a hosting provider with read access to the blob layer but not the server. It is the wrong choice when the threat model includes the server itself: a hosting compromise, a malicious admin, a subpoena reaching the running process.

Full encryption is the answer to that broader threat model, at the cost of per-user setup and a degraded experience for users who lose their encryption password without their recovery code. Managed encryption is what most installations want by default; full encryption is what installations with explicit confidentiality requirements reach for.
