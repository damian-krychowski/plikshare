ALTER TABLE u_users ADD COLUMN u_encryption_public_key BLOB NULL;
ALTER TABLE u_users ADD COLUMN u_encryption_encrypted_private_key BLOB NULL;
ALTER TABLE u_users ADD COLUMN u_encryption_kdf_salt BLOB NULL;
ALTER TABLE u_users ADD COLUMN u_encryption_kdf_params TEXT NULL;
ALTER TABLE u_users ADD COLUMN u_encryption_verify_hash BLOB NULL;
ALTER TABLE u_users ADD COLUMN u_encryption_recovery_wrapped_private_key BLOB NULL;
ALTER TABLE u_users ADD COLUMN u_encryption_recovery_verify_hash BLOB NULL;
