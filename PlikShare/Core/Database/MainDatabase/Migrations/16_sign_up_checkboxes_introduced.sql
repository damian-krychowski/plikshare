CREATE TABLE IF NOT EXISTS suc_sign_up_checkboxes
(
    suc_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    suc_text TEXT NOT NULL, 
    suc_is_required BOOLEAN NOT NULL,
    suc_is_deleted BOOLEAN NOT NULL
);

CREATE TABLE IF NOT EXISTS usuc_user_sign_up_checkboxes
(
    usuc_user_id INTEGER NOT NULL,
    usuc_sign_up_checkbox_id INTEGER NOT NULL,

    PRIMARY KEY (usuc_user_id, usuc_sign_up_checkbox_id),
    FOREIGN KEY (usuc_user_id) REFERENCES u_users (u_id),
    FOREIGN KEY (usuc_sign_up_checkbox_id) REFERENCES suc_sign_up_checkboxes (suc_id)
);