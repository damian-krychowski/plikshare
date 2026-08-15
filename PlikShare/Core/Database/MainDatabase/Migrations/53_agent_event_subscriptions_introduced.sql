-- Per-agent opt-out filter for the agent event inbox. This is low-churn configuration, so it stays
-- in the main database next to the agent config tables (like the tool-config/override tables) where
-- referential integrity to the agent matters. The high-churn delivered events live separately, in
-- the events database.
--
-- Semantics: absence of a row means the agent receives every agent-relevant event for the scopes it
-- can access. A row with aes_is_enabled = FALSE mutes that whole event category for the agent.
CREATE TABLE IF NOT EXISTS aes_agent_event_subscriptions
(
    aes_agent_id INTEGER NOT NULL,
    aes_event_category TEXT NOT NULL,

    aes_is_enabled BOOLEAN NOT NULL,

    PRIMARY KEY (aes_agent_id, aes_event_category),
    FOREIGN KEY (aes_agent_id) REFERENCES a_agents (a_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS index__aes_agent_event_subscriptions__aes_agent_id ON aes_agent_event_subscriptions (aes_agent_id);
