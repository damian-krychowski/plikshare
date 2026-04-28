# Full-Encryption in Plikshare

A technical description of full encryption mode in Plikshare: the key hierarchy, the file frame format, the metadata envelope, and the recovery mechanisms.

## Design goals

Full-encryption mode is built around three properties:

1. **Resilient to database and storage compromise.** An attacker who obtains the SQLite database, the file storage, or both simultaneously cannot read file contents, file names, folder names, or audit log details. The database holds only ciphertext, wrapped keys, public keys, salts, and verify hashes. Storage holds only encrypted file frames.

2. **Encryption scoping.** Encryption keys are scoped hard to the resources a user has access to inside the application. A user who only has access to one workspace holds key material that decrypts that workspace and nothing else — possessing their unwrapped private key does not unlock data they were never granted access to. The goal is to limit the blast radius of combined attacks: a database or storage compromise plus a phished encryption password from one user must not cascade into a global decryption.

3. **Recoverable from a recovery code alone.** If the database is lost, a user holding a recovery code can still decrypt files left in storage. The recovery code is the disaster-recovery root — no DB row, no admin assistance, no backup of any wrapped key is required.

### Out of scope

An attacker with **live access to the server process**, capable of dumping process memory while keys are unwrapped, is out of scope. 

Any in-process cryptography requires keys in plaintext at the moment of use. Mitigation focuses on minimizing this window: unwrapped keys are held in protected memory (pinned, mlocked, zeroed on dispose), released at the end of the request that needed them, and kept off the GC heap where possible.

## User identities

Every user who wants to use full-encryption features in Plikshare has to set up an `Encryption Password`. Setting it generates an X25519 keypair (public + private) and a 32-byte recovery seed. The password and the recovery seed are each used for two things: deriving a verify-hash (so the password or recovery code can be checked at unlock time without trying an unwrap) and deriving a KEK that wraps the private key. The private key is never stored in the database in plaintext — only its wrapped forms are.

The recovery seed is encoded as a 24-word BIP-39 mnemonic and returned to the user exactly once. It is never persisted server-side. The user is responsible for storing the mnemonic somewhere safe — it is the only way to recover their private key if they forget the encryption password.

```
┌──────────────────────────┐
│   Password               │
│   (user input)           │
└────────────┬─────────────┘
             │
             │  Argon2id (3 iter, 64 MiB, parallelism 1)
             ▼
┌──────────────────────────┐
│  User Encryption KEK     │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  AEAD-wrap
             ▼
┌──────────────────────────┐
│  User Private Key        │
│  (X25519) [32 bytes,     │
│  in DB wrapped]          │
└──────────────────────────┘
```

```
┌──────────────────────────┐
│  Recovery bytes          │
│  (random 32 bytes)       │
└────────────┬─────────────┘
             │
             │  HKDF-SHA256, info = ("plikshare-user-encryption-recovery-kek\0")
             ▼
┌──────────────────────────┐
│  User Encryption KEK     │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  AEAD-wrap
             ▼
┌──────────────────────────┐
│  User Private Key        │
│  (X25519) [32 bytes,     │
│  in DB wrapped]          │
└──────────────────────────┘
```

## Sealed box

The X25519 keypair is the foundation of how full-encrypted workspaces are shared. Each user has one keypair and one encryption password — a single password unlocks every workspace ever shared with them. The asymmetric construction has a second useful property: workspaces can be shared with a user offline. The inviter only needs the recipient's public key, which lives in the database in plaintext. Access to full-encryption resources rests on this pattern — every user holds their own sealed copies of the keys they need, and unsealing any of them requires only their private key.

The keypair is X25519, used in a sealed-box construction (the same primitive libsodium exposes as `crypto_box_seal`). Cryptographic primitives across the codebase come from .NET's `AesGcm` and `HKDF`, plus NSec's `ChaCha20Poly1305` and `X25519`. Wrapping a DEK to a recipient:

```
1. Generate ephemeral X25519 keypair (eph_priv, eph_pub).
2. shared = X25519(eph_priv, recipient_pub).
3. Derive AEAD key = HKDF-SHA256(
        ikm    = shared,
        salt   = empty,
        info   = "plikshare-sealed-box-v1\0",
        length = 32)
4. Encrypt payload with ChaCha20-Poly1305 under that key.
5. Output envelope: eph_pub(32) | nonce(12) | ciphertext | tag(16)
```

## User encryption session

To access full-encryption resources, the user must unlock their private key with their `encryption-password`. Unlock does not write to the database — it verifies the password and derives the private key into a session.

### Inputs (from `u_users.EncryptionMetadata`)

- `KdfSalt` — Argon2id salt, unique per user
- `KdfParams` — Argon2id parameters (memory, iterations, parallelism)
- `VerifyHash` — derived from the KEK, used to check the password without attempting an unwrap
- `EncryptedPrivateKey` — user's X25519 private key, AEAD-wrapped with the KEK

### Flow 
```
┌──────────────────────────┐
│   Encryption-Password    │
│     (from user input)    │
└───────────┬──────────────┘
            │
            │  Argon2id(password, KdfSalt, KdfParams)
            ▼
┌──────────────────────────┐
│  User Encryption KEK     │
│  [32 bytes, ephemeral]   │
└───────────┬──────────────┘
            │
            │  ComputeVerifyHash(KEK)
            ▼
┌──────────────────────────┐
│   constant-time compare  │
│   vs stored VerifyHash   │
└──────┬────────────┬──────┘
       │            │
match  │            │  mismatch
       │            │
       │            ▼
       │    ┌──────────────────┐
       │    │     reject       │
       │    │ (InvalidPassword)│
       │    └──────────────────┘
       ▼
┌──────────────────────────┐
│  AEAD-unwrap(KEK,        │
│  EncryptedPrivateKey)    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  User Private Key        │
│  (X25519)                │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ UserEncryptionSession-   │
│ Cookie (server-encrypted,│
│ scoped to ExternalId)    │
└──────────────────────────┘
```
The KEK is zeroed after use. The private key lives only inside the session cookie (encrypted with built-in .NET Data Protection module) — never persisted in plaintext server-side.

### Session lifetime

The unwrapped private key is sealed into `UserEncryptionSessionCookie` (server-side encryption, bound to `ExternalId`) and returned to the browser. On each subsequent request that touches a full-encryption resource, the cookie is decrypted server-side to obtain the private key, which is then used to unwrap the relevant workspace DEK.

`POST /api/user-encryption-password/lock` deletes the cookie. There is no server-side session state to clear.

### Workspace DEK access

The private key on its own does not decrypt anything — it exists to unseal wrapped DEKs. Every request that touches a full-encrypted workspace has to turn that private key into a set of Workspace DEKs for the workspace it is operating on, one per Storage DEK version that has ever applied to files in it. Those DEKs land in a `WorkspaceEncryptionSession` parked in `HttpContext.Items` for the rest of the request.

A user can reach a workspace's DEKs through two paths:

- **Member path** — a wrap in `wek_workspace_encryption_keys` sealed to the user's public key when they were added to the workspace. Unsealing it with the private key yields the Workspace DEK directly. This is the path used by workspace creators (who get a `wek` row at creation time) and by invited members.

- **Storage-owner path** — a wrap in `sek_storage_encryption_keys` sealed to the user's public key when they were granted storage access. Unsealing yields the Storage DEK; the Workspace DEK is then derived on the fly from `(Storage DEK, workspace_salt)`. This is the same derivation an offline recovery tool would walk from the seed — a storage owner can read every file in every workspace on their storage without ever being added as a workspace member.

```
┌──────────────────────────┐
│  User private key        │
│  (from session cookie)   │
└────────────┬─────────────┘
             │
             ├───────────────────────────────┐
             │                               │
             │  unseal wek_*                 │  unseal sek_*
             │  (member path)                │  (storage-owner path)
             ▼                               ▼
┌──────────────────────────┐    ┌──────────────────────────┐
│  Workspace DEK           │    │  Storage DEK             │
│  (one per version)       │    └────────────┬─────────────┘
└────────────┬─────────────┘                 │
             │                               │  HKDF-SHA256,
             │                               │  salt = workspace_salt
             │                               ▼
             │                  ┌──────────────────────────┐
             │                  │  Workspace DEK           │
             │                  │  (derived)               │
             │                  └────────────┬─────────────┘
             │                               │
             └───────────────┬───────────────┘
                             ▼
              ┌──────────────────────────────┐
              │  WorkspaceEncryptionSession  │
              │  (HttpContext.Items, scoped  │
              │   to the request)            │
              └──────────────────────────────┘
```

If both paths come up empty, the user is authenticated and unlocked but is not a member of the workspace and not an owner of its storage — the request is rejected before reaching the handler. If the cookie is missing or fails to unprotect, the request is rejected earlier still, before any unseal attempt.

The session's lifetime is bound to the HTTP request. The unsealed private key is wiped as soon as the unwrap finishes; the unsealed Workspace DEKs sit in protected memory and are zeroed when the response is flushed, via a dispose hook registered on the response. Nothing the unlock produces survives past the request that produced it — every subsequent request starts from the cookie again.

### Encryption password recovery

If a user forgets their `encryption-password`, the only way to regain access to their private key is through the recovery code they received as a 24-word BIP-39 mnemonic when the encryption-password was first set. Recovery seed is never stored in the database — the user is responsible for keeping the mnemonic safe.

The recovery seed is therefore as sensitive as the password itself: anyone holding it can reset the password and take over the encryption session without ever knowing the current password. Treating the mnemonic as a password-equivalent secret is essential — losing control of it is equivalent to losing control of the account.

#### Inputs (from `u_users.EncryptionMetadata`)

- `RecoveryVerifyHash` — derived from the recovery-KEK, used to check the recovery seed without attempting an unwrap
- `RecoveryWrappedPrivateKey` — user's X25519 private key, AEAD-wrapped with the recovery-KEK
- `PublicKey` — preserved across recovery, never rotated

The user provides their 24-word BIP-39 mnemonic (decoded into a 32-byte recovery seed) and a new `encryption-password`.

#### Flow

```
┌──────────────────────────┐
│  24-word BIP-39 mnemonic │
│  (from user input)       │
└────────────┬─────────────┘
             │
             │  RecoveryCodeCodec.TryDecode
             ▼
┌──────────────────────────┐
│  Recovery seed           │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  HKDF-SHA256, info = ("plikshare-user-encryption-recovery-kek\0")
             ▼
┌──────────────────────────┐
│  Recovery KEK            │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  ComputeVerifyHash(Recovery KEK)
             ▼
┌──────────────────────────┐
│   constant-time compare  │
│ vs RecoveryVerifyHash    │
└──────┬────────────┬──────┘
       │            │
match  │            │  mismatch
       │            │
       │            ▼
       │    ┌──────────────────────┐
       │    │       reject         │
       │    │ (InvalidRecoveryCode)│
       │    └──────────────────────┘
       ▼
┌──────────────────────────┐
│  AEAD-unwrap(            │
│  Recovery KEK,           │
│  RecoveryWrappedPrivate- │
│  Key)                    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  User Private Key        │
│  (X25519)                │
└────────────┬─────────────┘
             │
             │  Argon2id(new password, new KdfSalt, KdfParams)
             ▼
┌──────────────────────────┐
│  New User Encryption KEK │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  AEAD-wrap(new KEK, private key)
             │  ComputeVerifyHash(new KEK)
             ▼
┌──────────────────────────┐
│  Persist to              │
│  u_users.Encryption-     │
│  Metadata:               │
│  - new KdfSalt           │
│  - new KdfParams         │
│  - new VerifyHash        │
│  - new EncryptedPrivate- │
│    Key                   │
└──────────────────────────┘
```

After recovery, the previous `encryption-password` is permanently invalidated — the old `KdfSalt`, `KdfParams`, `VerifyHash` and `EncryptedPrivateKey` are overwritten. The recovery side is preserved unchanged: `RecoveryVerifyHash` and `RecoveryWrappedPrivateKey` are written back as-is, so the same 24-word mnemonic stays valid for future recoveries. The user's X25519 keypair (and therefore access to all previously shared workspaces) is preserved — only the password-side wrapping is rotated.

The unwrapped private key is returned to the caller, which seals it into `UserEncryptionSessionCookie` — the user is logged into the encryption session immediately after a successful reset, without a separate unlock step.

## Storage key hierarchy

The encryption mechanism is built around a hierarchy of derived keys. Starting from a single random seed (generated when a storage is created), HKDF produces a tree of keys that isolate workspaces from each other within a storage (one storage can hold many workspaces) and, in the future, boxes from each other within a workspace (one workspace can hold many boxes).

The shape of the hierarchy is:

`SEED + N × random salts → derived key`

With this construction, holding the seed is enough to decrypt any item encrypted under a key further down the tree. Holding a Storage DEK is enough to reach every workspace inside it. Holding a Workspace DEK will, once boxes ship, be enough to reach every box inside it.

```
            ┌──────────────────────────┐
            │  Seed                    │
            │  [32 random bytes]       │
            └────────────┬─────────────┘
                         │
                         │  HKDF-SHA256, info = ("plikshare-dek\0" || version_be32)
                         ▼
            ┌──────────────────────────┐
            │  Storage DEK v{version}  │
            └────────────┬─────────────┘
                         │
                         │  HKDF-SHA256, salt = workspace_salt
                         ▼
            ┌──────────────────────────┐
            │  Workspace DEK           │
            └──────┬────────────┬──────┘
                   │            │
HKDF-SHA256,       │            │
salt = file_salt   │            │  (same key reused)
                   ▼            ▼
    ┌────────────────────┐  ┌──────────────────────┐
    │  File DEK          │  │  Metadata DEK        │
    │  [AES-256-GCM key  │  │  (same as            │
    │  per file]         │  │  Workspace DEK)      │
    └────────────────────┘  └──────────────────────┘
```

File frames and metadata envelopes both reserve space for `N × salt` in their headers. Beyond enabling recovery, this opens the door to write-only keys in the future: a key derived from one extra salt below its parent scope (e.g. `Workspace DEK + random salt`) lets a user write new files but not read existing ones — the user cannot recover the parent key from their own key plus the salt, so anything encrypted under the parent stays opaque to them.

Recoverability is achieved by returning the seed used to derive the Storage DEK as a BIP-39 mnemonic — the same construction described in the storage creation flow.

## Storage Creation Flow

Creating a full-encryption storage is the moment the entire key hierarchy comes into existence. A single random seed is generated, and from it everything else follows: the Storage DEK, the recovery verify hash, the recovery code returned to the creator. The flow has one hard precondition — the creator must already have an encryption keypair. The freshly generated DEK has to be sealed to a public key before the request ends; otherwise it exists only in process memory and is lost the moment the request returns.

```
┌──────────────────────────┐
│  Recovery seed           │
│  [32 random bytes,       │
│   ephemeral]             │
└────────────┬─────────────┘
             │
             ├───────────────────────────────┬──────────────────────────────┐
             │                               │                              │
             │  HKDF-SHA256,                 │  ComputeVerifyHash           │  RecoveryCodeCodec
             │  info = ("plikshare-dek\0"    │                              │  (BIP-39, 24 words)
             │       || version_be32(0))     │                              │
             ▼                               ▼                              ▼
┌──────────────────────────┐   ┌──────────────────────────┐   ┌──────────────────────────┐
│  Storage DEK v0          │   │  RecoveryVerifyHash      │   │  Recovery code           │
└────────────┬─────────────┘   │  (persisted)             │   │  (returned to creator    │
             │                 └──────────────────────────┘   │   exactly once)          │
             │                                                └──────────────────────────┘
             │  sealed-box to each recipient public key
             ▼
┌───────────────────────────────┐
│  sek_storage_encryption_keys  │
│  (one row per creator         │
│   + each app owner            │
│   with a keypair)             │
└───────────────────────────────┘
```

The seed feeds three independent derivations and is then zeroed. It is never persisted. What survives in the database is the verify hash (so a future recovery attempt can be validated without trying an unwrap) and the wrapped DEK (so normal operation never needs to touch the seed). The seed itself leaves the server only as the 24-word mnemonic returned in the response.

The DEK is sealed not just to the creator but to every existing app owner who already has an encryption keypair. Owners without a keypair are skipped — the sealed-box construction needs a public key, and they have none. They get their wrap later, after they set up their encryption password, when an existing admin re-seals the same DEK to their newly created public key. Membership in `sek_storage_encryption_keys` is built up over time; creation only seeds it with whoever happens to qualify on day one.

The recovery code is returned in the response exactly once. There is no endpoint to fetch it later, no copy held server-side, no row to recover it from. From the moment the response is flushed, the only copies of the recovery seed in existence are the ones the creator wrote down.

## Storage Recovery codes

The recovery code returned at storage creation is the disaster-recovery root for the entire storage. It is not used during normal operation — day-to-day decryption goes through `sek_storage_encryption_keys`, where the Storage DEK is already wrapped to each admin's public key and unsealed on demand. The recovery code matters only in one scenario: the database is gone, and with it every wrapped DEK.

In that scenario the file storage may still be intact — S3 buckets, local volumes, whatever the backend was — full of encrypted file frames that nobody can read. Every V2 file header carries the Storage DEK version and the chain of salts down to the per-file AES key, so the only missing input is the Storage DEK itself. The recovery code is exactly that input.

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
│  Storage DEK v{v}        │
└────────────┬─────────────┘
             │
             │  for each file: read header,
             │  walk chain salts to
             │  Workspace DEK, then HKDF
             │  with FILE_SALT
             ▼
┌──────────────────────────┐
│  Per-file AES key        │
│  → decrypt frame         │
└──────────────────────────┘
```

The same derivation that produced Storage DEK v0 at creation time produces it again from the same seed. Every chain step below it — workspace salt, eventually box salt and others — lives in the file's own header. A file plus the recovery seed is enough to derive its AES key without consulting any external state.

This recovery path is not yet exposed in the product — no endpoint accepts a recovery code, no UI prompts for it. The intended consumer is an out-of-band tool that operates directly on the file storage: point it at a bucket, give it the 24-word mnemonic, and let it walk every encrypted frame, deriving keys from headers and emitting plaintext. No database, no running server, no admin credentials — only the storage and the seed.

Designing for it now matters because recoverability is a property of the format, not the tooling. Every V2 file already carries the format version, the Storage DEK version, and the full chain of salts — a tool written years from now decrypts files written today. Storage rotation falls out of this for free: `DeriveDek(seed, v)` is deterministic for any `v`, so a single mnemonic re-derives every version, and files under v0, v1, v2 all decrypt from the same seed.

What the recovery code cannot reconstruct is anything that lived only in the database — workspace membership, audit history, share links, user accounts. Recovery is a path back to the bytes, not back to the application state.

## Workspace Creation Flow

A workspace is the unit of access in full-encryption mode — its DEK is what gets sealed to members, and what every file inside is encrypted under at runtime. Creating a workspace on a full-encrypted storage is therefore the moment a new Workspace DEK comes into existence and gets wrapped to its first recipient: the creator.

The flow has three preconditions, all of which must hold before any DEK derivation happens. The creator must have an active encryption session — their X25519 private key has to be available to unwrap the parent Storage DEK. The creator must have an encryption keypair set up, otherwise there is no public key to seal the new Workspace DEK to. And the creator must already hold a wrapped Storage DEK row in `sek_storage_encryption_keys` for the target storage — only storage admins do, and a non-admin cannot derive workspace keys against a storage they have no parent key for.

```
┌──────────────────────────┐
│  Creator's private key   │
│  (from session cookie)   │
└────────────┬─────────────┘
             │
             │  unseal
             ▼
┌──────────────────────────┐
│  Creator's wrapped       │
│  Storage DEK v{latest}   │
│  (from sek_storage_      │
│   encryption_keys)       │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  Storage DEK v{latest}   │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  HKDF-SHA256,
             │  salt = workspace_salt
             │  (freshly random, 32 bytes)
             ▼
┌──────────────────────────┐
│  Workspace DEK           │
│  [32 bytes, ephemeral]   │
└────────────┬─────────────┘
             │
             │  sealed-box to creator's public key
             ▼
┌───────────────────────────────┐
│  wek_workspace_encryption_    │
│  keys                         │
│  one row: (workspace,         │
│   creator, storage_dek_       │
│   version, wrapped DEK)       │
└───────────────────────────────┘
```

The `workspace_salt` is generated freshly per workspace and persisted on the workspace row. From this point on it is the salt that defines the workspace as a cryptographic scope: every file written into the workspace will carry this salt as the single step in its V2 header chain, and every member added later will receive their own sealed-box wrap of the same Workspace DEK derived from it.

The Workspace DEK is always derived from the **latest** Storage DEK version available to the creator. Older versions exist only to keep files written before a past rotation decryptable — new derivations always go through the newest parent. The version used is recorded on the `wek` row so the read side can later line up wraps with files that were written under that version.

A non-admin cannot reach this flow — without a row in `sek_storage_encryption_keys` for the target storage there is no Storage DEK to unseal, and the request is rejected before any workspace state is touched. The only Storage DEKs a user can unseal are the ones already wrapped to their public key, so they can never create workspaces on a storage they were not granted access to.

After creation, the creator can immediately use the workspace: their session holds the private key that unwraps the `wek` row, which yields the Workspace DEK that decrypts every file and every metadata field. Adding members later is purely a re-wrap operation against the same Workspace DEK — no derivation, no file ciphertext touched.

## File Encryption Flow

By the time a file handler runs, the Workspace DEKs it needs are already in memory — unsealed by the encryption filter described in [Workspace DEK access](#workspace-dek-access).

A workspace can hold multiple Workspace DEKs at once — one per Storage DEK version that has ever been used for files in it. After a Storage DEK rotation, files written under v0 keep their v0-derived Workspace DEK and files written under v1 use the v1-derived one; both wraps live in `wek_workspace_encryption_keys` and both get unsealed into the session. Encryption uses the latest version. Decryption reads the version byte from the file header and asks the session for the matching DEK.

### Runtime path

Encrypting a new file:

```
┌──────────────────────────┐
│  WorkspaceEncryption-    │
│  Session                 │
│  (populated by filter)   │
└────────────┬─────────────┘
             │  GetLatestDek()
             ▼
┌──────────────────────────┐
│  Workspace DEK           │
│  (latest version)        │
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

The file's header is written out alongside: format version `0x02`, the Storage DEK version the Workspace DEK was derived from, the chain step salts (for now one entry: the workspace salt), the freshly random `FILE_SALT`, and the freshly random `NONCE_PREFIX`. The same fields are also persisted on the file's row in the database. The header carries everything a future reader needs to find the right key — whether that reader is the live application or an offline recovery tool.

The duplication into the database row is what makes range reads cheap: to decrypt a segment from the middle of a large file, the server reads `STORAGE_DEK_VERSION`, the chain step salts, `FILE_SALT` and `NONCE_PREFIX` straight from the row, without first issuing a storage round-trip just to fetch the header bytes. The in-file copy still matters — it is what lets an offline recovery tool decrypt a file with no database at all — but on the hot path, the database row is the source of truth.

Decrypting an existing file:

```
┌──────────────────────────┐
│  File header             │
│  (read from storage)     │
└────────────┬─────────────┘
             │
             │  parse STORAGE_DEK_VERSION,
             │  FILE_SALT, NONCE_PREFIX
             ▼
┌──────────────────────────┐
│  WorkspaceEncryption-    │
│  Session                 │
│  .GetDekForVersion(v)    │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  Workspace DEK           │
│  (matching version)      │
└────────────┬─────────────┘
             │
             │  HKDF-SHA256,
             │  salt = FILE_SALT
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

The runtime path uses the Workspace DEK directly as IKM — the chain step salts in the header are not consulted, because the session already holds the unsealed DEK. They exist for a different consumer: an offline recovery tool with only the seed and the file. That tool re-derives the Storage DEK from the seed, walks the chain salts to the Workspace DEK, then takes the same final HKDF step with `FILE_SALT`. Same destination, two paths — one fast and database-backed, one slow and self-contained.

`FILE_SALT` is freshly random per file. Two files in the same workspace share a Workspace DEK but have independent AES keys, and the same plaintext written twice produces two unrelated ciphertexts.

### File frame format

Files use a streaming AEAD adapted from [Google Tink](https://developers.google.com/tink/streaming-aead). Two format versions coexist and serve different modes:

- **V1** — the format used by workspaces with **managed encryption**, where the server holds the keys. The header is fixed-size and assumes a single server-managed key context.

- **V2** — the format used by workspaces with **full-encryption**. The header carries an explicit format-version byte, a Storage DEK version, and a serialized key-derivation chain, so the file is self-describing across rotations and future scope levels.

### Why streaming

Three reasons: large files, HTTP `Range:` requests, and parallel uploads. A single AES-GCM encryption forces the entire plaintext through memory and provides no random access. Streaming AEAD splits the file into segments, each independently authenticated. Any segment can be decrypted without touching the others. The same property works in reverse on upload: parts can be encrypted and pushed to storage in parallel, which lines up cleanly with S3-compatible multi-part upload APIs offered by most providers.

```
Segment size:           1 MiB (1,048,576 bytes)
Segments per file part: 10
Max payload per part:   10 MiB
```

10 MiB file parts align with the multi-part upload split.

### V1 frame layout (used in managed-encryption)

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

### V2 frame layout (used in full-encryption)

V2 introduces an explicit format-version byte and a derivation chain:

```
First segment:
┌──────────────────────────────────────────────────────────────────────┐
│ HEADER (variable) │           CIPHERTEXT            │   TAG (16 B)   │
└──────────────────────────────────────────────────────────────────────┘

Header = FORMAT_VERSION(1) = 0x02
       | STORAGE_DEK_VERSION(1)
       | CHAIN_STEPS_COUNT(1)
       | N × STEP_SALT (32 each)
       | FILE_SALT(32)
       | NONCE_PREFIX(7)

Header size = 42 + 32·N bytes

Subsequent segments (same as V1):
┌──────────────────────────────────────────────────────────────────────┐
│                       CIPHERTEXT                    │   TAG (16 B)   │
└──────────────────────────────────────────────────────────────────────┘
```

The additional bytes buy three properties:

- **Self-describing format.** Byte 0 of the file says "this is format 2". Future formats get byte 0 = 0x03 without breaking changes.

- **Self-describing key version.** The reader knows which Storage DEK to fetch without consulting the database. This is what makes a file portable across rotations.

- **Self-describing derivation chain.** `CHAIN_STEPS_COUNT` plus `N × STEP_SALT` fully encode the path from Storage DEK down to the file's AES key. Reader plus DB give the key; reader plus recovery code give the key. No external table required.

Every V2 file currently ships with `CHAIN_STEPS_COUNT = 1`, where the single step salt is the workspace salt. Encoding the workspace salt into the file header is what makes recovery from a recovery code alone tractable: without it, the chain step from Storage DEK to Workspace DEK would have a missing input that lives only in the database. The chain machinery generalizes to N steps so that future scope levels (per-box, per-share-link) can be appended to the chain without changing the format.

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

The header bytes do not need to be fetched on the hot path: `STORAGE_DEK_VERSION`, the chain step salts, `FILE_SALT` and `NONCE_PREFIX` are already on the file's database row. Given those fields and the requested plaintext range, the server computes which segments cover the range, fetches just those segment ranges from storage, authenticates and decrypts each independently, and yields plaintext into the response stream with the trim offsets applied.

## Metadata encryption: the `pse:` prefix

File contents are the obvious target for encryption. File names, folder names, upload metadata are the less obvious one — all of them leak the names and structure of stored data if kept in the clear.

In full-encryption mode, every text metadata field is encrypted with AES-256-GCM under the Workspace DEK and stored as base64 with a self-identifying prefix:

```
pse:<base64( FORMAT(1) = 0x01
           | KEY_VERSION(1)
           | CHAIN_STEPS_COUNT(1)
           | N × STEP_SALT(32)
           | NONCE(12)
           | CIPHERTEXT
           | TAG(16) )>
```

The column type stays `TEXT` regardless of whether the workspace is encrypted — same column, different content. The `pse:` prefix is a hard self-identifying tag: any tooling, any DB dump, any decode routine can immediately tell encrypted metadata from plaintext. Request validation rejects user-supplied metadata starting with `pse:`, so the prefix is never ambiguous.

A fresh 12-byte random nonce per encrypted value means the same plaintext encrypts to a different ciphertext every time. Equality between two encrypted values reveals nothing.

Whether a value is encrypted depends on whether the workspace is in full-encryption mode. If it is not, the value is stored as-is. If it is, the value is encrypted under the latest Workspace DEK version and emitted with the `pse:` prefix. The decision is made at write time based on the workspace's mode, not on the column or the caller.

### Search over ciphertext

Search on full-encrypted workspaces is available to users who hold the relevant Workspace DEKs in their session. It is implemented by registering a SQLite user-defined function `app_decrypt_metadata` that recognizes encrypted text by the `pse:` prefix and decrypts the envelope using a Workspace DEK looked up from the request's session map (keyed by `workspace_id`).

```sql
SELECT *
FROM   fi_files
WHERE  fi_workspace_id IN (...)
  AND  app_decrypt_metadata(fi_workspace_id, fi_name) LIKE :pattern
```

The session map is built once at the start of the request: every workspace the searching user has access to has its DEKs unsealed and held in protected memory. The UDF runs row-by-row inside the query engine, so decrypted plaintext lives only inside the function callback for the duration of the row's evaluation.

There is no index, so this is a full scan over the workspace's metadata. Workspaces are bounded, so the scan runs at I/O speed.

## Audit logs

The audit log records every meaningful action in the system: workspace created, member invited, file uploaded, folder renamed, link generated, and so on. Each entry carries a structured `details` JSON payload — for a rename, the old and new names; for an upload, the file path; for a member invitation, the email of the invitee. In full-encryption mode that payload is a problem. A workspace whose files are encrypted at rest still leaks every name they ever held to anyone who can read the audit log table directly.

The fix is to encrypt the sensitive fields of the `details` payload with the same envelope used for file metadata: AES-256-GCM under the Workspace DEK, base64-encoded, prefixed with `pse:`. Same primitive, same key, same prefix discipline. There is no separate audit-log key — the cryptographic principle is that whoever has access to a workspace's contents has access to its history, and vice versa.

What stays in plaintext is the structural skeleton of the entry: event type (`workspace.member-invited`, `file.uploaded`, `auth.signed-in-failed`), severity, the actor's id, the timestamp, the workspace and storage references. What gets encrypted is the content-bearing strings inside `details` — file names, folder paths, comment bodies, link names. This split is deliberate: an admin can run "show me every failed login in the past week" or "every bulk delete in workspace W" without unlocking any encryption session, because the answers come from plaintext columns. But "what was the file called that Alice renamed yesterday" needs the Workspace DEK.

```
┌──────────────────────────────────────────────────────────┐
│  audit log entry                                         │
│                                                          │
│  event_type:    "file.renamed"        ◀── plaintext      │
│  actor_user:    User#42               ◀── plaintext      │
│  workspace:     Workspace#7           ◀── plaintext      │
│  timestamp:     2026-04-01T13:22Z     ◀── plaintext      │
│  severity:      "info"                ◀── plaintext      │
│                                                          │
│  details:       {                                        │
│    "oldName":   "pse:AQEB…"           ◀── encrypted      │
│    "newName":   "pse:AQEB…"           ◀── encrypted      │
│    "fileId":    "fi_abc123"           ◀── plaintext      │
│  }                                                       │
└──────────────────────────────────────────────────────────┘
```

Decryption happens lazily, per-entry. The list view returns only the plaintext header fields — event type, actor, workspace, timestamp, severity — and never touches the `details` payload at all. Encrypted content is only fetched and decoded when an admin opens a specific entry. At that point the server resolves which workspace the entry belongs to, opens a `WorkspaceEncryptionSession` for it, walks the `details` JSON tree recursively, and rewrites every string value starting with `pse:` to its decrypted form. Strings without the prefix are left alone; numbers, booleans, and structural keys are never touched.

If the admin has no encryption session at all, the request returns 423 — the same response the rest of the application uses for missing unlock. If the admin has a session but no access to the workspace the entry concerns — they are an app admin but not a member of that workspace and not an owner of its storage — the encrypted strings are replaced with the literal `[encrypted]` rather than decoded. The entry is still visible, the structural information is still readable, but the content stays opaque. Application-admin role does not grant cryptographic access; that has to come through the same wek/sek wraps as any other read.

## Invitations: the bootstrapping problem

Sealing a Workspace DEK to a user requires that user's public key. New users do not have one yet. This creates a chicken-and-egg problem in the invitation flow: an inviter wants to add Alice to a workspace now, but Alice has not yet set an encryption password, so she has no keypair, so nothing can be sealed to her.

The solution: ephemeral keys. Two tables (migration 28):

```sql
CREATE TABLE euek_ephemeral_user_encryption_keys (
    euek_user_id              INTEGER NOT NULL,
    euek_public_key           BLOB    NOT NULL,
    euek_encrypted_private_key BLOB   NOT NULL,
    euek_created_at           TEXT    NOT NULL,
    PRIMARY KEY (euek_user_id)
);

CREATE TABLE ewek_ephemeral_workspace_encryption_keys (
    ewek_workspace_id        INTEGER NOT NULL,
    ewek_user_id             INTEGER NOT NULL,
    ewek_storage_dek_version INTEGER NOT NULL,
    ewek_encrypted_workspace_dek BLOB NOT NULL,
    ewek_created_at          TEXT    NOT NULL,
    ewek_expires_at          TEXT    NOT NULL,
    PRIMARY KEY (ewek_workspace_id, ewek_user_id, ewek_storage_dek_version)
);
```

Flow:

```
1. Inviter adds Alice to workspace W.
2. Server generates an ephemeral X25519 keypair for Alice.
   - Public key: stored.
   - Private key: encrypted under a key derived from Alice's invitation code.
3. Inviter seals Workspace DEK[v] to Alice's ephemeral public key.
   - Lands in ewek_* with a TTL.
4. Alice receives an invitation link containing the invitation code.
5. Alice clicks the link and sets an encryption password.
6. Server uses the invitation code to decrypt the ephemeral private key.
7. The ephemeral private key unwraps the ephemeral Workspace DEK wraps.
8. Alice generates her real X25519 keypair.
9. Workspace DEKs are re-wrapped to Alice's real public key — landing in wek_*.
10. Ephemeral rows are deleted.
```

The TTL on `ewek_expires_at` is the safety net: if Alice never accepts, a cleanup job deletes the rows and the inviter has to re-invite. The sealed-box mechanism means the inviter does not need Alice's eventual private key — only a public key to seal to, even an ephemeral one.

The symmetric wrap of the ephemeral private key under the code-derived KEK lasts only as long as the invitation code does.

## Membership revocation

Removing a user from a workspace, or cancelling a pending invitation, comes down to deleting wraps. The user's keypair is not touched, the Workspace DEK is not touched, file ciphertext is not touched — what changes is which wraps exist in the database.

For a user who already accepted the invitation, removal deletes their `wek_workspace_encryption_keys` rows for the workspace they are being removed from. Without a wrap, no new request can produce a `WorkspaceEncryptionSession` for that workspace — the next request fails the unseal step and is rejected. The same applies when a user leaves a workspace voluntarily, or when their account is deleted entirely (in which case all `wek_*` and `sek_*` rows for that user are dropped together with the user row).

For a user who has not yet accepted, cancellation deletes the corresponding `ewek_ephemeral_workspace_encryption_keys` rows. The ephemeral private key in `euek_ephemeral_user_encryption_keys` may stay until the invitation TTL expires, but with no `ewek_*` rows to unwrap, it has nothing to act on.

## Storage DEK rotation

The format and key hierarchy were designed for rotation, but rotation itself is not yet implemented. There is currently exactly one Storage DEK version per storage — version 0 — and no mechanism to introduce a new one.

What the format already supports:

- The V2 file header carries `STORAGE_DEK_VERSION`, so files written under different versions coexist without ambiguity.
- `wek_workspace_encryption_keys` has `wek_storage_dek_version` in its primary key, allowing one workspace member to hold wraps under multiple versions simultaneously.
- `WorkspaceEncryptionSession` indexes its DEKs by version and exposes `GetDekForVersion(v)`, so decryption picks the right key per file.
- `DeriveDek(seed, v)` is parameterized by version, so a single recovery seed re-derives every past Storage DEK on demand.

What is missing is the operation itself: the act of generating a new version, sealing it to every storage admin, and switching new writes over to it. When rotation is added, existing files keep their version byte and remain decryptable under the old DEK; new files will be written under the new DEK, and the read path will continue to dispatch by version with no further changes.

## Planned extensions

The chain in the V2 file header carries an arbitrary number of step salts, and the metadata envelope carries the same. Today the chain has one step (the workspace salt). Two future extensions reuse this structure rather than introducing new derivation logic.

### Boxes: cryptographic isolation of a workspace subset

A box is a subset of a workspace shared with users who do **not** have access to the workspace as a whole. The intent is cryptographic isolation: a user with access only to a box must be unable to read files outside the box, even if they obtain the ciphertext.

The plan:

```
1. When a box is created, a new 32-byte box_salt is generated.
2. BoxDEK = HKDF-SHA256(ikm = WorkspaceDEK, salt = box_salt).
3. Files created inside the box are encrypted with a chain of two step salts:
   [workspace_salt, box_salt]. CHAIN_STEPS_COUNT = 2 in the header.
4. Workspace members have WorkspaceDEK and can derive BoxDEK from the
   header salts — they read everything (workspace files and box files).
5. Box-only users have no WorkspaceDEK. To let them read files inside the
   box, two wraps are stored:
   - For each file in the box, the per-file AES key is wrapped to BoxDEK
     (one envelope per file).
   - For each box-only user, BoxDEK is sealed to their public key
     (one envelope per box per user).
```

Storage cost: one wrap per file plus one sealed BoxDEK per box-only user. A box-only user reads a file by unsealing the BoxDEK with their private key, then unwrapping the file's AES key with the BoxDEK.

Cryptographic isolation: a box-only user cannot read files outside the box because they hold only BoxDEK, not WorkspaceDEK, and the workspace files' AES keys are not wrapped to BoxDEK. Workspace members retain access to box files because they can derive BoxDEK from WorkspaceDEK and the header's box_salt, and from there unwrap any file in the box.

### Non-admin workspace creators

Today only admins create workspaces. Each workspace's chain is one step long: `[workspace_salt]`.

The plan is to allow non-admin users to hold a `create_workspace` permission. Such a user will not receive the Storage DEK directly. Instead, an admin will derive a per-creator key:

```
CreatorDEK = HKDF-SHA256(ikm = StorageDEK, salt = creator_salt)
```

and seal it to the creator's public key. When the creator creates a workspace, they add a workspace_salt of their own:

```
WorkspaceDEK = HKDF-SHA256(ikm = CreatorDEK, salt = workspace_salt)
            = HKDF-SHA256(
                ikm  = HKDF-SHA256(StorageDEK, creator_salt),
                salt = workspace_salt)
```

The chain in files inside such workspaces is two steps long: `[creator_salt, workspace_salt]`. Existing workspaces created by admins keep `[workspace_salt]` (one step). Both shapes are valid V2 headers — `CHAIN_STEPS_COUNT` already varies per file.

Admins still hold the Storage DEK and can derive every CreatorDEK and every WorkspaceDEK by walking the chain salts from the header. Recovery from the recovery seed alone works the same way: the seed re-derives the Storage DEK, the header provides every salt down to the per-file AES key.