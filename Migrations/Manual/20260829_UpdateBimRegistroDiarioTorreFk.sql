-- Migración DDL transaccional para alinear bim_registro_diario al modelo de torres (torre_id -> bim_proyecto_torre)

BEGIN;

-- 1. Renombrar columna zona_id a torre_id si aún figura como zona_id
DO $$ 
BEGIN 
    IF EXISTS(SELECT 1 FROM information_schema.columns WHERE table_name='bim_registro_diario' AND column_name='zona_id') THEN 
        ALTER TABLE bim_registro_diario RENAME COLUMN zona_id TO torre_id; 
    END IF; 
END $$;

-- 2. Eliminar constraints obsoletas
ALTER TABLE bim_registro_diario DROP CONSTRAINT IF EXISTS bim_registro_diario_zona_id_fkey;
ALTER TABLE bim_registro_diario DROP CONSTRAINT IF EXISTS fk_bim_registro_diario_zona;
ALTER TABLE bim_registro_diario DROP CONSTRAINT IF EXISTS fk_bim_registro_diario_torre;

-- 3. Crear llave foránea limpia apuntando a bim_proyecto_torre (id)
ALTER TABLE bim_registro_diario ADD CONSTRAINT fk_bim_registro_diario_torre 
    FOREIGN KEY (torre_id) REFERENCES bim_proyecto_torre (id) ON DELETE CASCADE;

-- 4. Renombrar índice si corresponde
ALTER INDEX IF EXISTS ix_bim_registro_diario_zona_id RENAME TO ix_bim_registro_diario_torre_id;

COMMIT;
