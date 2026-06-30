-- ============================================================================
-- Seed retroactivo: crea checklists obligatorios (tipo_activacion='automatico')
-- para todos los proyectos activos que aún no los tienen.
-- Idempotente: el UNIQUE INDEX (proyecto_id, plantilla_id) previene duplicados,
-- y el NOT EXISTS en el WHERE lo confirma.
-- ============================================================================
BEGIN;

-- ── Paso 1: insertar ss_checklist_proyecto faltantes ─────────────────────────
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
  AND pl.es_obligatorio = true
  AND pl.tipo_activacion = 'automatico'
  AND pl.activo = true
  AND NOT EXISTS (
      SELECT 1 FROM ss_checklist_proyecto cp
      WHERE cp.proyecto_id = pr.project_id
        AND cp.plantilla_id = pl.id
  );

-- ── Paso 2: insertar ss_checklist_proyecto_item para los recién creados ───────
INSERT INTO ss_checklist_proyecto_item
    (checklist_proyecto_id, plantilla_item_id, completado, created_at, updated_at)
SELECT
    cp.id,
    pi.id,
    false,
    NOW(),
    NOW()
FROM ss_checklist_proyecto cp
JOIN ss_checklist_plantilla_item pi ON pi.plantilla_id = cp.plantilla_id
WHERE pi.activo = true
  AND NOT EXISTS (
      SELECT 1 FROM ss_checklist_proyecto_item ci
      WHERE ci.checklist_proyecto_id = cp.id
        AND ci.plantilla_item_id = pi.id
  );

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT
    pr.project_description   AS proyecto,
    pl.nombre                AS plantilla,
    cp.estado,
    COUNT(ci.id)             AS total_items
FROM ss_checklist_proyecto cp
JOIN project                pr ON pr.project_id   = cp.proyecto_id
JOIN ss_checklist_plantilla pl ON pl.id           = cp.plantilla_id
LEFT JOIN ss_checklist_proyecto_item ci ON ci.checklist_proyecto_id = cp.id
GROUP BY pr.project_description, pl.nombre, cp.estado, pl.orden
ORDER BY pr.project_description, pl.orden;
