ALTER TABLE fi_files ADD COLUMN fi_encryption_chain_salts BLOB NULL;
ALTER TABLE fi_files ADD COLUMN fi_encryption_format_version INTEGER NULL;
ALTER TABLE fu_file_uploads ADD COLUMN fu_encryption_chain_salts BLOB NULL;
ALTER TABLE fu_file_uploads ADD COLUMN fu_encryption_format_version INTEGER NULL;
