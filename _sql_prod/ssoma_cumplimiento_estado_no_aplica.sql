-- ============================================================================
-- Módulo SSOMA — Cumplimiento: agrega estado "no aplica" (además de cumplido/
-- pendiente), para actividades que no corresponden según la etapa del proyecto.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

ALTER TABLE ss_cumplimiento_registro
    ADD COLUMN IF NOT EXISTS estado VARCHAR(20) NOT NULL DEFAULT 'pendiente';

ALTER TABLE ss_cumplimiento_registro
    ADD COLUMN IF NOT EXISTS motivo_no_aplica TEXT NULL;

-- Backfill: filas existentes ya reflejaban cumplido/pendiente en la columna booleana.
UPDATE ss_cumplimiento_registro
SET estado = CASE WHEN cumplido THEN 'cumplido' ELSE 'pendiente' END
WHERE estado = 'pendiente';

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT estado, COUNT(*) FROM ss_cumplimiento_registro GROUP BY estado;
