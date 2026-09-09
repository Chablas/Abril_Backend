-- ============================================================================
-- Backfill: crea la plantilla de checklist para cada partida que ya existía
-- antes de que la creación de partida generara su plantilla automáticamente.
-- Queda vacía (sin ítems) — se va llenando con el tiempo desde la UI.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- ============================================================================
BEGIN;

-- ── Paso 1: una plantilla por cada partida que aún no tiene ninguna ─────────
INSERT INTO ss_checklist_plantilla
    (nombre, descripcion, tipo_activacion, evento_activacion, es_obligatorio, orden, activo, partida_id, created_at, updated_at)
SELECT p.nombre, NULL, 'automatico', NULL, true, p.orden, true, p.id, NOW(), NOW()
FROM ss_checklist_partida p
WHERE NOT EXISTS (SELECT 1 FROM ss_checklist_plantilla pl WHERE pl.partida_id = p.id);

-- ── Paso 2: activar esas plantillas (vacías) en todos los proyectos activos ─
INSERT INTO ss_checklist_proyecto
    (proyecto_id, plantilla_id, estado, porcentaje_completado,
     fecha_activacion, activado_por_id, notificacion_enviada, created_at, updated_at)
SELECT
    pr.project_id,
    pl.id,
    'pendiente',
    0,
    NOW(),
    NULL,
    false,
    NOW(),
    NOW()
FROM project pr
CROSS JOIN ss_checklist_plantilla pl
WHERE pr.active = true AND pr.estado = 'ACTIVO'
  AND pl.partida_id IS NOT NULL
  AND pl.es_obligatorio = true
  AND pl.tipo_activacion = 'automatico'
  AND pl.activo = true
  AND NOT EXISTS (
      SELECT 1 FROM ss_checklist_proyecto cp
      WHERE cp.proyecto_id = pr.project_id
        AND cp.plantilla_id = pl.id
  );

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT p.nombre AS partida, pl.nombre AS plantilla, pl.id AS plantilla_id,
       (SELECT COUNT(*) FROM ss_checklist_proyecto cp WHERE cp.plantilla_id = pl.id) AS proyectos_con_checklist
FROM ss_checklist_partida p
JOIN ss_checklist_plantilla pl ON pl.partida_id = p.id
ORDER BY p.orden;
