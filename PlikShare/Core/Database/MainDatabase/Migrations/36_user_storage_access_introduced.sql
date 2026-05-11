ALTER TABLE u_users ADD COLUMN u_storage_access_mode TEXT NOT NULL DEFAULT 'all';

CREATE TABLE IF NOT EXISTS usa_user_storage_access
(
    usa_user_id INTEGER NOT NULL,
    usa_storage_id INTEGER NOT NULL,

    PRIMARY KEY (usa_user_id, usa_storage_id),
    FOREIGN KEY (usa_user_id) REFERENCES u_users (u_id) ON DELETE CASCADE,
    FOREIGN KEY (usa_storage_id) REFERENCES s_storages (s_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__usa_user_storage_access__usa_user_id
    ON usa_user_storage_access (usa_user_id);

CREATE INDEX IF NOT EXISTS index__usa_user_storage_access__usa_storage_id
    ON usa_user_storage_access (usa_storage_id);
