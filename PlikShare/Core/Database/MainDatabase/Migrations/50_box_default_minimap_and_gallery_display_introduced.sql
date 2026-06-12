ALTER TABLE bo_boxes ADD COLUMN bo_default_minimap_enabled BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE bo_boxes ADD COLUMN bo_default_gallery_layout TEXT NOT NULL DEFAULT 'justified';
ALTER TABLE bo_boxes ADD COLUMN bo_default_gallery_tile_size TEXT NOT NULL DEFAULT 'medium';
