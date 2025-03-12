CREATE TABLE IF NOT EXISTS u_users
(
    u_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    u_external_id TEXT NOT NULL,
    u_user_name TEXT NOT NULL,
    u_normalized_user_name TEXT NOT NULL,
    u_email TEXT NOT NULL,
    u_normalized_email TEXT NOT NULL,
    u_email_confirmed BOOLEAN NOT NULL,
    u_password_hash TEXT,
    u_security_stamp TEXT NOT NULL,
    u_concurrency_stamp TEXT NOT NULL,
    u_phone_number TEXT,
    u_phone_number_confirmed BOOLEAN NOT NULL,
    u_two_factor_enabled BOOLEAN NOT NULL,
    u_lockout_end TEXT,
    u_lockout_enabled BOOLEAN NOT NULL,
    u_access_failed_count INTEGER NOT NULL,
    u_is_invitation BOOLEAN NOT NULL,
    u_invitation_code TEXT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS unique__u_users__u_normalized_email ON u_users (u_normalized_email);
CREATE UNIQUE INDEX IF NOT EXISTS unique__u_users__u_external_id ON u_users (u_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS unique__u_users__u_normalized_user_name ON u_users (u_normalized_user_name);
CREATE UNIQUE INDEX IF NOT EXISTS unique__u_users__u_invitation_code ON u_users (u_invitation_code);
    
CREATE TABLE IF NOT EXISTS r_roles
(
    r_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    r_external_id TEXT NOT NULL,
    r_name TEXT NOT NULL,
    r_normalized_name TEXT NOT NULL,
    r_concurrency_stamp TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS unique__r_roles__r_normalized_name ON r_roles (r_normalized_name);
    
CREATE TABLE IF NOT EXISTS ut_user_tokens
(
    ut_user_id INTEGER NOT NULL,
    ut_login_provider TEXT NOT NULL,
    ut_name TEXT NOT NULL,
    ut_value TEXT,

    PRIMARY KEY (ut_user_id, ut_login_provider, ut_name),
    FOREIGN KEY (ut_user_id) REFERENCES u_users (u_id)
);

CREATE TABLE IF NOT EXISTS ur_user_roles
(
    ur_user_id INTEGER NOT NULL,
    ur_role_id INTEGER NOT NULL,

    PRIMARY KEY (ur_user_id, ur_role_id),
    FOREIGN KEY (ur_user_id) REFERENCES u_users (u_id),
    FOREIGN KEY (ur_role_id) REFERENCES r_roles (r_id)
);

CREATE TABLE IF NOT EXISTS ul_user_logins
(
    ul_login_provider TEXT NOT NULL,
    ul_provider_key TEXT NOT NULL,
    ul_provider_display_name TEXT,
    ul_user_id INTEGER NOT NULL,

    PRIMARY KEY (ul_login_provider, ul_provider_key),
    FOREIGN KEY (ul_user_id) REFERENCES u_users (u_id)
);

CREATE INDEX IF NOT EXISTS index__ul_user_logins__user_id ON ul_user_logins (ul_user_id);
    
CREATE TABLE IF NOT EXISTS uc_user_claims
(
    uc_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    uc_user_id INTEGER NOT NULL,
    uc_claim_type TEXT NOT NULL,
    uc_claim_value TEXT NOT NULL,

    FOREIGN KEY (uc_user_id) REFERENCES u_users (u_id)
);

CREATE INDEX IF NOT EXISTS index__uc_user_claims__user_id ON uc_user_claims (uc_user_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__uc_user_claims__user_type_value ON uc_user_claims (uc_user_id,uc_claim_type,uc_claim_value);
    
CREATE TABLE IF NOT EXISTS rc_role_claims
(
    rc_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    rc_role_id INTEGER NOT NULL,
    rc_claim_type TEXT NOT NULL,
    rc_claim_value TEXT NOT NULL,

    FOREIGN KEY (rc_role_id) REFERENCES r_roles (r_id)
);

CREATE INDEX IF NOT EXISTS index__rc_role_claims__role_id ON rc_role_claims (rc_role_id);
    
CREATE TABLE IF NOT EXISTS as_app_settings
(
    as_key TEXT NOT NULL PRIMARY KEY,
    as_value TEXT
);
    
CREATE TABLE IF NOT EXISTS q_queue
(
    q_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    q_job_type TEXT NOT NULL,
    q_definition TEXT NOT NULL,
    q_status TEXT NOT NULL,
    q_failed_retries_count INTEGER NOT NULL,
    q_execute_after_date TEXT NOT NULL,
    q_processing_started_at TEXT,
    q_completed_at TEXT,
    q_failed_at TEXT,
    q_enqueued_at TEXT NOT NULL,
    q_correlation_id TEXT NOT NULL,
    q_debounce_id TEXT
);

CREATE INDEX IF NOT EXISTS index__q_queue__q_status ON q_queue (q_status);
CREATE INDEX IF NOT EXISTS index__q_queue__q_execute_after_date ON q_queue (q_execute_after_date);
CREATE UNIQUE INDEX IF NOT EXISTS index__q_queue__q_debounce_id ON q_queue (q_debounce_id);
    
CREATE TABLE IF NOT EXISTS qc_queue_completed
(
    qc_id INTEGER NOT NULL PRIMARY KEY,
    qc_job_type TEXT NOT NULL,
    qc_definition TEXT NOT NULL,
    qc_failed_retries_count INTEGER NOT NULL,
    qc_enqueued_at TEXT NOT NULL,
    qc_execute_after_date TEXT NOT NULL,
    qc_completed_at TEXT NOT NULL,
    qc_correlation_id TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS s_storages
(
    s_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    s_external_id TEXT NOT NULL,

    s_type TEXT NOT NULL,
    s_name TEXT NOT NULL,
    s_details_encrypted BLOB NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS index__s_storages__s_external_id ON s_storages (s_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__s_storages__s_name ON s_storages (s_name);
    
CREATE TABLE IF NOT EXISTS w_workspaces
(
    w_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    w_external_id TEXT NOT NULL,

    w_owner_id INTEGER NOT NULL,
    w_storage_id INTEGER NOT NULL,

    w_name TEXT NOT NULL,
    w_current_size_in_bytes INTEGER NOT NULL,

    w_bucket_name TEXT NOT NULL,
    w_is_bucket_created BOOLEAN NOT NULL,

    w_is_being_deleted BOOLEAN NOT NULL,

    FOREIGN KEY (w_owner_id) REFERENCES u_users (u_id),
    FOREIGN KEY (w_storage_id) REFERENCES s_storages (s_id)
);

CREATE INDEX IF NOT EXISTS index__w_workspaces__w_owner_id ON w_workspaces (w_owner_id);
CREATE INDEX IF NOT EXISTS index__w_workspaces__w_storage_id ON w_workspaces (w_storage_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__w_workspaces__w_external_id ON w_workspaces (w_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__w_workspaces__w_bucket_name ON w_workspaces (w_bucket_name);
    
CREATE TABLE IF NOT EXISTS wm_workspace_membership
(
    wm_workspace_id INTEGER NOT NULL,
    wm_member_id INTEGER NOT NULL,

    wm_inviter_id INTEGER NOT NULL,
    wm_was_invitation_accepted BOOLEAN NOT NULL,

    wm_allow_share BOOLEAN NOT NULL,
    wm_created_at TEXT NOT NULL,

    PRIMARY KEY (wm_workspace_id, wm_member_id),
    FOREIGN KEY (wm_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (wm_member_id) REFERENCES u_users (u_id),
    FOREIGN KEY (wm_inviter_id) REFERENCES u_users (u_id)
);

CREATE INDEX IF NOT EXISTS index__wm_workspace_membership__wm_member_id ON wm_workspace_membership (wm_member_id);
CREATE INDEX IF NOT EXISTS index__workspace_membership__wm_inviter_id ON wm_workspace_membership (wm_inviter_id);
CREATE INDEX IF NOT EXISTS index__workspace_membership__wm_workspace_id ON wm_workspace_membership (wm_workspace_id);
    
CREATE TABLE IF NOT EXISTS fo_folders
(
    fo_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    fo_external_id TEXT NOT NULL,
    fo_workspace_id INTEGER NOT NULL,
    fo_parent_folder_id INTEGER,
    fo_ancestor_folder_ids JSON,
    fo_name TEXT NOT NULL,
    fo_is_being_deleted BOOLEAN NOT NULL,

    FOREIGN KEY (fo_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (fo_parent_folder_id) REFERENCES fo_folders (fo_id)
);

CREATE INDEX IF NOT EXISTS index__fo_folders__fo_workspace_id ON fo_folders (fo_workspace_id);
CREATE INDEX IF NOT EXISTS index__fo_folders__fo_parent_folder_id ON fo_folders (fo_parent_folder_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__fo_folders__fo_external_id ON fo_folders (fo_external_id);
    
CREATE TABLE IF NOT EXISTS fu_file_uploads
(
    fu_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    fu_external_id TEXT NOT NULL,
    fu_workspace_id INTEGER NOT NULL,
    fu_folder_id INTEGER NULL,
    fu_s3_upload_id TEXT NOT NULL,

    fu_owner_identity_type TEXT NOT NULL,
    fu_owner_identity TEXT NOT NULL,

    fu_file_name TEXT NOT NULL,
    fu_file_extension TEXT NOT NULL,
    fu_file_content_type TEXT NOT NULL,
    fu_file_size_in_bytes INTEGER NOT NULL,
    fu_part_size_in_bytes INTEGER NOT NULL,
    fu_file_external_id TEXT NOT NULL,
    fu_file_s3_key_secret_part TEXT NOT NULL,

    FOREIGN KEY (fu_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (fu_folder_id) REFERENCES fo_folders (fo_id)
);

CREATE INDEX IF NOT EXISTS index__fu_file_uploads__fu_workspace_id ON fu_file_uploads (fu_workspace_id);
CREATE INDEX IF NOT EXISTS index__fu_file_uploads__fu_owner_identity__owner_identity_type ON fu_file_uploads (fu_owner_identity_type, fu_owner_identity);
CREATE INDEX IF NOT EXISTS index__fu_file_uploads__fu_folder_id ON fu_file_uploads (fu_folder_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__fu_file_uploads__fu_external_id ON fu_file_uploads (fu_external_id);
    
CREATE TABLE IF NOT EXISTS fup_file_upload_parts
(
    fup_file_upload_id INTEGER NOT NULL,
    fup_part_number INTEGER NOT NULL,

    fup_etag TEXT NULL,

    PRIMARY KEY (fup_file_upload_id, fup_part_number),
    FOREIGN KEY (fup_file_upload_id) REFERENCES fu_file_uploads (fu_id)
);

CREATE INDEX IF NOT EXISTS index__fup_file_upload_parts__fup_file_upload_id ON fup_file_upload_parts (fup_file_upload_id);
    
CREATE TABLE IF NOT EXISTS fi_files
(
    fi_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    fi_external_id TEXT NOT NULL,
    fi_workspace_id INTEGER NOT NULL,
    fi_folder_id INTEGER,

    fi_s3_key_secret_part TEXT NOT NULL,

    fi_name TEXT NOT NULL,
    fi_extension TEXT NOT NULL,
    fi_content_type TEXT NOT NULL,
    fi_size_in_bytes INTEGER NOT NULL,

    fi_is_upload_completed BOOLEAN NOT NULL,

    fi_uploader_identity_type TEXT NOT NULL,
    fi_uploader_identity TEXT NOT NULL,

    FOREIGN KEY (fi_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (fi_folder_id) REFERENCES fo_folders (fo_id)
);

CREATE INDEX IF NOT EXISTS index__fi_files__fi_workspace_id ON fi_files (fi_workspace_id);
CREATE INDEX IF NOT EXISTS index__fi_files__fi_folder_id ON fi_files (fi_folder_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__fi_files__fi_external_id ON fi_files(fi_external_id);
CREATE INDEX IF NOT EXISTS index__fi_file_revisions__fi_uploader_identity_and_type ON fi_files (fi_uploader_identity_type, fi_uploader_identity);
    
CREATE TABLE IF NOT EXISTS bo_boxes
(
    bo_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    bo_external_id TEXT NOT NULL,
    bo_workspace_id INTEGER NOT NULL,
    bo_folder_id INTEGER NULL,

    bo_is_enabled BOOLEAN NOT NULL,
    bo_name TEXT NOT NULL,

    bo_header_is_enabled BOOLEAN NOT NULL,
    bo_header_json JSON NULL,
    bo_header_html TEXT NULL,

    bo_footer_is_enabled BOOLEAN NOT NULL,
    bo_footer_json JSON NULL,
    bo_footer_html TEXT NULL,

    bo_is_being_deleted BOOLEAN NOT NULL,

    FOREIGN KEY (bo_workspace_id) REFERENCES w_workspaces (w_id),
    FOREIGN KEY (bo_folder_id) REFERENCES fo_folders (fo_id)
);

CREATE INDEX IF NOT EXISTS index__bo_boxes__bo_workspace_id ON bo_boxes (bo_workspace_id);
CREATE INDEX IF NOT EXISTS index__bo_boxes__bo_folder_id ON bo_boxes (bo_folder_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__bo_boxes__bo_external_id ON bo_boxes (bo_external_id);
    
CREATE TABLE IF NOT EXISTS bl_box_links
(
    bl_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    bl_external_id TEXT NOT NULL,
    bl_box_id INTEGER NOT NULL,

    bl_is_enabled BOOLEAN NOT NULL,
    bl_name TEXT NOT NULL,
    bl_access_code TEXT NOT NULL,

    bl_allow_download BOOLEAN NOT NULL,
    bl_allow_upload BOOLEAN NOT NULL,
    bl_allow_list BOOLEAN NOT NULL,
    bl_allow_delete_file BOOLEAN NOT NULL,
    bl_allow_rename_file BOOLEAN NOT NULL,
    bl_allow_move_items BOOLEAN NOT NULL,
    bl_allow_create_folder BOOLEAN NOT NULL,
    bl_allow_delete_folder BOOLEAN NOT NULL,
    bl_allow_rename_folder BOOLEAN NOT NULL,

    FOREIGN KEY (bl_box_id) REFERENCES bo_boxes (bo_id)
);

CREATE INDEX IF NOT EXISTS index__bl_box_links__bl_box_id ON bl_box_links (bl_box_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__bl_box_links__bl_external_id ON bl_box_links (bl_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__bl_box_links__bl_access_code ON bl_box_links (bl_access_code);
    
CREATE TABLE IF NOT EXISTS bm_box_membership
(
    bm_box_id INTEGER NOT NULL,
    bm_member_id INTEGER NOT NULL,

    bm_inviter_id INTEGER NOT NULL,
    bm_was_invitation_accepted BOOLEAN NOT NULL,

    bm_allow_download BOOLEAN NOT NULL,
    bm_allow_upload BOOLEAN NOT NULL,
    bm_allow_list BOOLEAN NOT NULL,
    bm_allow_delete_file BOOLEAN NOT NULL,
    bm_allow_rename_file BOOLEAN NOT NULL,
    bm_allow_move_items BOOLEAN NOT NULL,
    bm_allow_create_folder BOOLEAN NOT NULL,
    bm_allow_delete_folder BOOLEAN NOT NULL,
    bm_allow_rename_folder BOOLEAN NOT NULL,

    bm_created_at TEXT NOT NULL,

    PRIMARY KEY (bm_box_id, bm_member_id),
    FOREIGN KEY (bm_box_id) REFERENCES bo_boxes (bo_id)  ,
    FOREIGN KEY (bm_member_id) REFERENCES u_users (u_id),
    FOREIGN KEY (bm_inviter_id) REFERENCES u_users (u_id)
);

CREATE INDEX IF NOT EXISTS index__bm_box_membership__bm_box_id ON bm_box_membership (bm_box_id);
CREATE INDEX IF NOT EXISTS index__bm_box_membership__bm_member_id ON bm_box_membership (bm_member_id);
CREATE INDEX IF NOT EXISTS index__bm_box_membership__bm_inviter_id ON bm_box_membership (bm_inviter_id);
    
CREATE TABLE IF NOT EXISTS ep_email_providers
(
    ep_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ep_external_id TEXT NOT NULL,
    ep_is_active BOOLEAN NOT NULL,

    ep_type TEXT NOT NULL,
    ep_name TEXT NOT NULL,
    ep_email_from TEXT NOT NULL,
    ep_details_encrypted BLOB NOT NULL,
    ep_confirmation_code TEXT NOT NULL,
    ep_is_confirmed BOOLEAN NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS index__ep_email_providers__ep_external_id ON ep_email_providers (ep_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS index__ep_email_providers__ep_name ON ep_email_providers (ep_name);