BEGIN;

-- Eliminar constraints antiguas
ALTER TABLE bim_bloqueo DROP CONSTRAINT IF EXISTS fk_bim_bloqueo_zona;
ALTER TABLE bim_bloqueo DROP CONSTRAINT IF EXISTS fk_bim_bloqueo_zona_nivel;
ALTER TABLE bim_bloqueo DROP CONSTRAINT IF EXISTS fk_bim_bloqueo_zona_sector;
ALTER TABLE bim_bloqueo DROP CONSTRAINT IF EXISTS fk_bim_bloqueo_bim_proyecto_torre_zona_id;
ALTER TABLE bim_bloqueo DROP CONSTRAINT IF EXISTS fk_bim_bloqueo_bim_torre_nivel_zona_nivel_id;
ALTER TABLE bim_bloqueo DROP CONSTRAINT IF EXISTS fk_bim_bloqueo_bim_zona_sector_zona_sector_id;

-- Renombrar columnas
ALTER TABLE bim_bloqueo RENAME COLUMN zona_id TO torre_id;
ALTER TABLE bim_bloqueo RENAME COLUMN zona_nivel_id TO nivel_id;
ALTER TABLE bim_bloqueo RENAME COLUMN zona_sector_id TO sector;

-- Limpiar datos heredados de sector para evitar IDs obsoletos
UPDATE bim_bloqueo SET sector = NULL;

-- Índices
ALTER INDEX IF EXISTS ix_bim_bloqueo_zona_id RENAME TO ix_bim_bloqueo_torre_id;
ALTER INDEX IF EXISTS ix_bim_bloqueo_zona_nivel_id RENAME TO ix_bim_bloqueo_nivel_id;
DROP INDEX IF EXISTS ix_bim_bloqueo_zona_sector_id;

-- Recrear llaves foráneas limpias
ALTER TABLE bim_bloqueo ADD CONSTRAINT fk_bim_bloqueo_bim_proyecto_torre_torre_id 
    FOREIGN KEY (torre_id) REFERENCES bim_proyecto_torre (id) ON DELETE SET NULL;
    
ALTER TABLE bim_bloqueo ADD CONSTRAINT fk_bim_bloqueo_bim_torre_nivel_nivel_id 
    FOREIGN KEY (nivel_id) REFERENCES bim_torre_nivel (id) ON DELETE SET NULL;

-- Registrar migración
INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20260829181016_UpdateBimBloqueoTorreNivelSector', '10.0.2')
ON CONFLICT DO NOTHING;

COMMIT;
