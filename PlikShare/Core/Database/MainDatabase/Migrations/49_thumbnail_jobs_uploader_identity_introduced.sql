UPDATE q_queue
SET q_definition = json_remove(
    json_set(
        q_definition,
        '$.uploaderIdentityType', 'user_external_id',
        '$.uploaderIdentity', json_extract(q_definition, '$.triggeredByUserExternalId')
    ),
    '$.triggeredByUserExternalId'
)
WHERE q_job_type = 'generate-image-thumbnails'
    AND json_extract(q_definition, '$.triggeredByUserExternalId') IS NOT NULL;
