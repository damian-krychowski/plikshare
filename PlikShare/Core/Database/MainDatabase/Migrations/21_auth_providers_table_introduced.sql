CREATE TABLE IF NOT EXISTS ap_auth_providers
(
    ap_id INTEGER PRIMARY KEY AUTOINCREMENT,
    ap_external_id TEXT NOT NULL UNIQUE,
    ap_name TEXT NOT NULL UNIQUE,
    ap_type TEXT NOT NULL,
    ap_is_active INTEGER NOT NULL,
    ap_client_id TEXT NOT NULL,
    ap_client_secret_encrypted BLOB NOT NULL,
    ap_issuer_url TEXT NOT NULL,
    ap_auto_discovery_url TEXT NOT NULL,
    ap_created_at TEXT NOT NULL
);
