-- ============================================================================
-- ITSE pasa a ser checklist obligatorio de activación automática.
-- Ejecutar en PRODUCCIÓN. Idempotente.
-- El backend ya activa automáticamente (al crear el proyecto) toda plantilla
-- con es_obligatorio=true y tipo_activacion='automatico'
-- (ChecklistRepository.SeedChecklistsObligatoriosAsync, invocado desde
-- ProjectRepository al crear/reactivar un proyecto). Este script solo ajusta
-- el dato y hace el backfill retroactivo para proyectos ya activos.
-- ============================================================================
BEGIN;

-- ── Paso 1: marcar ITSE (obra) como obligatorio + automático ────────────────
UPDATE ss_checklist_plantilla
SET es_obligatorio = true,
    tipo_activacion = 'automatico',
    evento_activacion = 'inicio_proyecto',
    updated_at = NOW()
WHERE nombre = 'ITSE';

-- ── Paso 2: backfill retroactivo para proyectos activos que no lo tienen ────
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
  AND pl.nombre = 'ITSE'
  AND pl.es_obligatorio = true
  AND pl.tipo_activacion = 'automatico'
  AND pl.activo = true
  AND NOT EXISTS (
      SELECT 1 FROM ss_checklist_proyecto cp
      WHERE cp.proyecto_id = pr.project_id
        AND cp.plantilla_id = pl.id
  );

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
JOIN ss_checklist_plantilla pl ON pl.id = cp.plantilla_id AND pl.nombre = 'ITSE'
WHERE pi.activo = true
  AND NOT EXISTS (
      SELECT 1 FROM ss_checklist_proyecto_item ci
      WHERE ci.checklist_proyecto_id = cp.id
        AND ci.plantilla_item_id = pi.id
  );

COMMIT;

-- ── Verificación ─────────────────────────────────────────────────────────────
SELECT pr.project_description AS proyecto, pl.nombre AS plantilla, cp.estado, COUNT(ci.id) AS total_items
FROM ss_checklist_proyecto cp
JOIN project pr ON pr.project_id = cp.proyecto_id
JOIN ss_checklist_plantilla pl ON pl.id = cp.plantilla_id AND pl.nombre = 'ITSE'
LEFT JOIN ss_checklist_proyecto_item ci ON ci.checklist_proyecto_id = cp.id
GROUP BY pr.project_description, pl.nombre, cp.estado
ORDER BY pr.project_description;
