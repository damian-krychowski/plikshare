CREATE TABLE IF NOT EXISTS aic_ai_conversations
(
    aic_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    aic_external_id TEXT NOT NULL,
    aic_integration_external_id INTEGER NOT NULL,
    aic_is_waiting_for_ai_response BOOLEAN NOT NULL,
    aic_name TEXT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS unique__aic_ai_conversations__aic_external_id ON aic_ai_conversations (aic_external_id);
   
CREATE TABLE IF NOT EXISTS aim_ai_messages
(
    aim_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    aim_external_id TEXT NOT NULL,
    aim_ai_conversation_id INTEGER NOT NULL,
    aim_conversation_counter INTEGER NOT NULL,
    
    aim_message_encrypted BLOB NOT NULL,
    aim_includes_encrypted BLOB NOT NULL,
    aim_ai_model TEXT NOT NULL,

    aim_user_identity_type TEXT NOT NULL,
    aim_user_identity TEXT NOT NULL,
    aim_created_at TEXT NOT NULL,
    
    FOREIGN KEY (aim_ai_conversation_id) REFERENCES aic_ai_conversations (aic_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS unique__aim_ai_messages__aim_external_id ON aim_ai_messages (aim_external_id);
CREATE UNIQUE INDEX IF NOT EXISTS unique__aim_ai_messages__aim_ai_conversation_id__aim_conversation_counter ON aim_ai_messages (aim_ai_conversation_id, aim_conversation_counter);
CREATE INDEX IF NOT EXISTS index__aim_ai_messages__aim_user_identity__aim_user_identity_type ON aim_ai_messages (aim_user_identity, aim_user_identity_type);     