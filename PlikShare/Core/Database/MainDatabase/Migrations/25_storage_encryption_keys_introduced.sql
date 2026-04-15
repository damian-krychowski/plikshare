CREATE TABLE sek_storage_encryption_keys (
    sek_storage_id INTEGER NOT NULL,
    sek_user_id INTEGER NOT NULL,
    sek_wrapped_storage_dek BLOB NOT NULL,
    sek_wrapped_at TEXT NOT NULL,
    sek_wrapped_by_user_id INTEGER NULL,
    PRIMARY KEY (sek_storage_id, sek_user_id),
    FOREIGN KEY (sek_storage_id) REFERENCES s_storages(s_id) ON DELETE CASCADE,
    FOREIGN KEY (sek_user_id) REFERENCES u_users(u_id) ON DELETE CASCADE,
    FOREIGN KEY (sek_wrapped_by_user_id) REFERENCES u_users(u_id) ON DELETE SET NULL
);

CREATE INDEX idx_sek_user_id ON sek_storage_encryption_keys (sek_user_id);
