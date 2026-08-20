-- ============================================================================
-- EMO — corregir programaciones automáticas que quedaron como "Ingreso"
-- cuando en realidad son una renovación (deberían ser "Periódico Anual")
-- Fecha: 2026-08-19
--
-- Causa (código ya corregido en EmoAutoProgramacionService.cs):
--   El auto-programador copiaba literalmente el tipo_emo_id del EMO que estaba
--   por vencer para la cita nueva. "Ingreso" tiene requiere_nuevo=true y
--   vigencia_meses=12 igual que "Periódico Anual", así que cuando un EMO de
--   Ingreso vencía, la cita de renovación salía tipo "Ingreso" otra vez en vez
--   de pasar a "Periódico Anual". Por definición, todo candidato del
--   auto-programador ya tenía al menos un WorkerEmo previo (el que está
--   venciendo), así que ninguna de estas filas es un Ingreso real.
--
-- Alcance: solo programaciones con origen='Automatico', tipo actual "Ingreso",
-- todavía activas (state=true, estado no terminal). No se tocan filas
-- Manual (las eligió una persona a propósito) ni las ya cerradas
-- (Completado/Cancelado/Rechazado por Clínica/No se presentó) — esas quedan
-- como historial.
-- ============================================================================

BEGIN;

CREATE TEMP TABLE tmp_emo_tipo_ingreso_fix AS
SELECT p.id AS programacion_id, p.worker_id, p.estado, p.fecha_programada,
       p.tipo_emo_id AS tipo_emo_id_actual,
       (SELECT t2.id FROM ss_emo_tipos t2
        WHERE t2.activo AND lower(t2.nombre) = 'periódico anual'
        LIMIT 1) AS tipo_emo_id_correcto
FROM ss_programacion_emos p
JOIN ss_emo_tipos t ON t.id = p.tipo_emo_id
WHERE p.state = true
  AND p.origen = 'Automatico'
  AND t.nombre = 'Ingreso'
  AND p.estado NOT IN ('Completado', 'Cancelado', 'Rechazado por Clínica', 'No se presentó');

-- 1) VERIFICACIÓN PREVIA — revisar esta lista antes de seguir. tipo_emo_id_correcto
--    no debe salir NULL en ninguna fila (si sale NULL, el catálogo no tiene
--    "Periódico Anual" activo y hay que resolverlo antes de continuar).
SELECT f.programacion_id, f.worker_id, per.full_name, f.estado, f.fecha_programada,
       f.tipo_emo_id_actual, f.tipo_emo_id_correcto
FROM tmp_emo_tipo_ingreso_fix f
JOIN workers w ON w.id = f.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
ORDER BY f.fecha_programada;

-- ----------------------------------------------------------------------------
-- 2) CORRECCIÓN — cambia el tipo de EMO de la programación, deja nota y
--    actualiza updated_at. No toca fecha, estado, clínica ni nada más.
-- ----------------------------------------------------------------------------
UPDATE ss_programacion_emos p
SET tipo_emo_id = f.tipo_emo_id_correcto,
    notas       = concat_ws(
                      E'\n',
                      nullif(p.notas, ''),
                      'Corregido 19/08/2026: quedó programado como "Ingreso" por un bug del auto-programador (ya corregido); corresponde a una renovación, se pasa a "Periódico Anual".'
                  ),
    updated_at  = now()
FROM tmp_emo_tipo_ingreso_fix f
WHERE p.id = f.programacion_id
  AND f.tipo_emo_id_correcto IS NOT NULL;

-- ----------------------------------------------------------------------------
-- 3) VERIFICACIÓN POSTERIOR — debe devolver 0 filas.
-- ----------------------------------------------------------------------------
SELECT p.id, p.worker_id, p.tipo_emo_id, p.estado
FROM ss_programacion_emos p
JOIN ss_emo_tipos t ON t.id = p.tipo_emo_id
WHERE p.state = true
  AND p.origen = 'Automatico'
  AND t.nombre = 'Ingreso'
  AND p.estado NOT IN ('Completado', 'Cancelado', 'Rechazado por Clínica', 'No se presentó');

DROP TABLE tmp_emo_tipo_ingreso_fix;

COMMIT;
