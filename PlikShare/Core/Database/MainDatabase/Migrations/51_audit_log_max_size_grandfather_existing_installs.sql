INSERT INTO as_app_settings (as_key, as_value)
SELECT 'audit-log-max-size-in-bytes', '-1'
WHERE EXISTS (SELECT 1 FROM u_users)
ON CONFLICT (as_key) DO NOTHING;
