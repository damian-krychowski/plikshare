-- Agent-created quick shares: the creating agent is recorded here, while qsh_creator_id keeps
-- the responsible human owner (for an agent it is the agent's owner). Additive, no table rebuild.
ALTER TABLE qsh_quick_shares ADD COLUMN qsh_creator_agent_id INTEGER NULL REFERENCES a_agents (a_id);

CREATE INDEX IF NOT EXISTS index__qsh__creator_agent_id
    ON qsh_quick_shares (qsh_creator_agent_id) WHERE qsh_creator_agent_id IS NOT NULL;
