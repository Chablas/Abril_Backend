-- ============================================================================
-- Gestión Administrativa · Salidas — Un Consolidado del S10 para VARIAS rendiciones
-- PASO 1 de 2 · ANTES de desplegar el backend
-- Fecha: 2026-09-11
--
-- Hasta hoy el Consolidado del S10 era 1 a 1 con la planilla de rendición
-- (ga_consolidado_s10.rendicion_id + un índice único parcial: un vigente por
-- planilla). Desde Gestión de Rendiciones ahora se seleccionan varias
-- rendiciones con la primera revisión aprobada y se les adjunta UN solo
-- consolidado, aunque sean de trabajadores distintos: la única condición es que
-- todos sean de una misma razón social (la valida el backend). Un consolidado
-- pasa a cubrir N planillas, así que el vínculo sale de la columna y va a una
-- tabla puente:
--
--   ga_consolidado_s10_rendicion (consolidado_s10_id, rendicion_id, state)
--
--   • Una planilla tiene a lo sumo UN vínculo vigente (índice único parcial).
--   • Reemplazar el consolidado no borra nada: los vínculos viejos quedan con
--     state = false y el consolidado viejo conserva la lista exacta de
--     planillas que cubrió (auditoría).
--
-- ORDEN DE EJECUCIÓN
--   1. ESTE script, ANTES de desplegar el backend: el backend nuevo lee la
--      tabla puente y sin ella las pantallas de rendiciones se caen (42P01).
--      El backend viejo sigue funcionando igual con este script aplicado.
--   2. 20260911_GaConsolidadoS10VariasRendiciones_PostDeploy.sql, DESPUÉS de
--      desplegar el backend: bota ga_consolidado_s10.rendicion_id, que el
--      backend viejo todavía lee.
--
-- Idempotente mientras exista rendicion_id (o sea, hasta correr el paso 2).
-- Aplicar en dev y prod.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ga_consolidado_s10_rendicion (
    id                 serial  PRIMARY KEY,
    consolidado_s10_id integer NOT NULL,
    rendicion_id       integer NOT NULL,
    state              boolean NOT NULL DEFAULT true,
    CONSTRAINT fk_ga_consolidado_s10_rendicion_consolidado
        FOREIGN KEY (consolidado_s10_id) REFERENCES ga_consolidado_s10(id),
    CONSTRAINT fk_ga_consolidado_s10_rendicion_rendicion
        FOREIGN KEY (rendicion_id) REFERENCES ga_rendicion(id)
);

-- Una planilla está en un solo consolidado vigente a la vez. El nombre NO es
-- ux_ga_consolidado_s10_rendicion_vigente porque ese ya lo usa el índice de la
-- columna vieja en ga_consolidado_s10: los nombres de índice son únicos por
-- esquema, y con IF NOT EXISTS la tabla nueva se habría quedado sin índice.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_consolidado_s10_rendicion_una_vigente
    ON ga_consolidado_s10_rendicion (rendicion_id) WHERE state;

-- Un consolidado vincula a cada planilla una sola vez. Sirve además de índice
-- para ir del consolidado a sus planillas.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_consolidado_s10_rendicion_par
    ON ga_consolidado_s10_rendicion (consolidado_s10_id, rendicion_id);

COMMENT ON TABLE ga_consolidado_s10_rendicion IS
    'Qué planillas de rendición cubre cada Consolidado del S10 (un consolidado puede agrupar '
    'varias, de una misma razón social). state=false = la planilla pasó a otro consolidado; '
    'como máximo un vínculo vigente por planilla.';

-- ── Backfill ────────────────────────────────────────────────────────────────
-- Primero, por si el script se vuelve a correr después de que el backend viejo
-- reemplazara algún consolidado: el vínculo de un consolidado dado de baja no
-- puede seguir vigente (le ocuparía el lugar al nuevo en el índice único).
UPDATE ga_consolidado_s10_rendicion x
SET    state = false
FROM   ga_consolidado_s10 c
WHERE  c.id = x.consolidado_s10_id
  AND  x.state
  AND  NOT c.state;

-- Cada consolidado de planilla pasa a la tabla puente con su mismo state. Los
-- de salida suelta (solicitud_id, registros antiguos) no tienen planilla que
-- vincular: se siguen leyendo por su columna.
INSERT INTO ga_consolidado_s10_rendicion (consolidado_s10_id, rendicion_id, state)
SELECT c.id, c.rendicion_id, c.state
FROM   ga_consolidado_s10 c
WHERE  c.rendicion_id IS NOT NULL
  AND  NOT EXISTS (
         SELECT 1 FROM ga_consolidado_s10_rendicion x
         WHERE  x.consolidado_s10_id = c.id
           AND  x.rendicion_id = c.rendicion_id);

-- ── ga_consolidado_s10 ──────────────────────────────────────────────────────
-- El CHECK de "ámbito único" exigía rendicion_id o solicitud_id. Los
-- consolidados nuevos no llevan ninguno de los dos: lo que cubren está en la
-- tabla puente. rendicion_id se queda (NULLABLE, como ya estaba) hasta el
-- paso 2, porque el backend viejo la lee y la escribe.
ALTER TABLE ga_consolidado_s10 DROP CONSTRAINT IF EXISTS chk_ga_consolidado_s10_ambito_unico;

COMMENT ON TABLE ga_consolidado_s10 IS
    'PDF Consolidado del S10. Cubre una o varias planillas de rendición (ver '
    'ga_consolidado_s10_rendicion) o, en registros antiguos, una sola salida (solicitud_id). '
    'state=false = versión reemplazada.';

-- ── Guarda ──────────────────────────────────────────────────────────────────
-- Todo consolidado de planilla vigente tiene que haber quedado con su vínculo
-- vigente: si no, su planilla dejaría de mostrarlo.
DO $$
DECLARE
    sin_vinculo integer;
BEGIN
    SELECT count(*) INTO sin_vinculo
    FROM   ga_consolidado_s10 c
    WHERE  c.state
      AND  c.rendicion_id IS NOT NULL
      AND  NOT EXISTS (
             SELECT 1 FROM ga_consolidado_s10_rendicion x
             WHERE  x.consolidado_s10_id = c.id
               AND  x.rendicion_id = c.rendicion_id
               AND  x.state);

    IF sin_vinculo > 0 THEN
        RAISE EXCEPTION 'Quedaron % consolidados vigentes sin su vinculo vigente: no se aplica nada.', sin_vinculo;
    END IF;
END $$;

COMMIT;
