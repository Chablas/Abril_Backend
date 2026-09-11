-- ============================================================================
-- Gestión Administrativa · Salidas — Un Consolidado del S10 para VARIAS rendiciones
-- PASO 2 de 2 · SOLO DESPUÉS DE DESPLEGAR EL BACKEND NUEVO
-- Fecha: 2026-09-11
--
-- Bota ga_consolidado_s10.rendicion_id: desde el paso 1
-- (20260911_GaConsolidadoS10VariasRendiciones.sql) el vínculo con la planilla
-- vive en ga_consolidado_s10_rendicion y el backend nuevo ya no lee ni escribe
-- la columna. El backend VIEJO sí la lee: correr esto antes del deploy deja
-- caídas Gestión de Rendiciones, Mis Rendiciones y Reembolsos (42703).
--
-- Antes de botarla vuelve a sincronizar la tabla puente, por si el backend viejo
-- adjuntó o reemplazó algún consolidado entre el paso 1 y el deploy.
--
-- Idempotente: si la columna ya no está, no hace nada. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE  table_schema = 'public'
          AND  table_name   = 'ga_consolidado_s10'
          AND  column_name  = 'rendicion_id'
    ) THEN
        -- Un consolidado dado de baja no puede conservar vínculos vigentes.
        UPDATE ga_consolidado_s10_rendicion x
        SET    state = false
        FROM   ga_consolidado_s10 c
        WHERE  c.id = x.consolidado_s10_id
          AND  x.state
          AND  NOT c.state;

        -- Lo que el backend viejo haya subido sin vínculo.
        INSERT INTO ga_consolidado_s10_rendicion (consolidado_s10_id, rendicion_id, state)
        SELECT c.id, c.rendicion_id, c.state
        FROM   ga_consolidado_s10 c
        WHERE  c.rendicion_id IS NOT NULL
          AND  NOT EXISTS (
                 SELECT 1 FROM ga_consolidado_s10_rendicion x
                 WHERE  x.consolidado_s10_id = c.id
                   AND  x.rendicion_id = c.rendicion_id);

        -- La columna se va con su FK y con el índice único parcial que la usaba
        -- (ux_ga_consolidado_s10_rendicion_vigente).
        ALTER TABLE ga_consolidado_s10 DROP COLUMN rendicion_id;
    END IF;
END $$;

COMMIT;
