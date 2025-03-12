-- 1. s_storages table changes
ALTER TABLE s_storages ADD COLUMN s_encryption_type TEXT NULL;
ALTER TABLE s_storages ADD COLUMN s_encryption_details_encrypted BLOB NULL;

-- 2. fu_file_uploads table changes
ALTER TABLE fu_file_uploads DROP COLUMN fu_part_size_in_bytes;

ALTER TABLE fu_file_uploads ADD COLUMN fu_encryption_key_version INTEGER NULL;
ALTER TABLE fu_file_uploads ADD COLUMN fu_encryption_salt BLOB NULL;
ALTER TABLE fu_file_uploads ADD COLUMN fu_encryption_nonce_prefix BLOB NULL;

-- 3. fi_files table changes
ALTER TABLE fi_files ADD COLUMN fi_encryption_key_version INTEGER NULL;
ALTER TABLE fi_files ADD COLUMN fi_encryption_salt BLOB NULL;
ALTER TABLE fi_files ADD COLUMN fi_encryption_nonce_prefix BLOB NULL;