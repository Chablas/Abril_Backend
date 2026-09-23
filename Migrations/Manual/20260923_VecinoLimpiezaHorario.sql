-- ============================================================================
-- Vecinos — Horario de las limpiezas del calendario.
--
-- Qué cambia y por qué
--
-- En un mismo día se visitan varios departamentos y la hora de cada visita se
-- escribía a mano en el detalle («DEPARTAMENTO 201 // DE 9 AM - 10 AM»). Ahora
-- la limpieza guarda su horario: hora de inicio y, si se quiere, hora de fin.
-- El modal del día las ordena por esa hora.
--
-- Las dos columnas son opcionales: las limpiezas ya registradas no tienen hora
-- y se quedan así. Es la hora local de la visita (time sin zona), no un
-- instante: no se convierte a UTC.
--
-- El CHECK repite la regla del backend: sin inicio no hay fin, y el fin va
-- después del inicio.
--
-- ORDEN DE EJECUCIÓN
--   • ANTES de desplegar. El backend nuevo lee hora_inicio/hora_fin: sin estas
--     columnas el calendario de limpiezas responde 42703 apenas se abre.
--   • Al revés no pasa nada: el backend que está corriendo hoy no las nombra.
--
-- Re-ejecutable: IF NOT EXISTS, y el CHECK solo se crea si no existe.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

ALTER TABLE vecino_limpieza ADD COLUMN IF NOT EXISTS hora_inicio time NULL;
ALTER TABLE vecino_limpieza ADD COLUMN IF NOT EXISTS hora_fin    time NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE  conname = 'ck_vecino_limpieza_horario'
        AND    conrelid = 'vecino_limpieza'::regclass
    ) THEN
        ALTER TABLE vecino_limpieza
            ADD CONSTRAINT ck_vecino_limpieza_horario
            CHECK (hora_fin IS NULL OR (hora_inicio IS NOT NULL AND hora_fin > hora_inicio));
    END IF;
END $$;

COMMIT;

-- ── Verificación (solo lectura) ─────────────────────────────────────────────
SELECT column_name, data_type, is_nullable
FROM   information_schema.columns
WHERE  table_name = 'vecino_limpieza'
AND    column_name IN ('hora_inicio', 'hora_fin')
ORDER  BY column_name;
