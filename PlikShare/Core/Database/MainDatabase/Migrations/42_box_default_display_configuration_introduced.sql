ALTER TABLE bo_boxes ADD COLUMN bo_default_view_mode TEXT NOT NULL DEFAULT 'list-view';
ALTER TABLE bo_boxes ADD COLUMN bo_default_sort_mode TEXT NOT NULL DEFAULT 'custom';
ALTER TABLE bo_boxes ADD COLUMN bo_default_sort_direction TEXT NOT NULL DEFAULT 'asc';
ALTER TABLE bo_boxes ADD COLUMN bo_default_thumbnails_enabled BOOLEAN NOT NULL DEFAULT FALSE;
