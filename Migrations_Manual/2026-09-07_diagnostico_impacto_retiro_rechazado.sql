-- ============================================================================
-- DIAGNÓSTICO (solo lectura) — dimensiona el impacto del cambio: cuántos
-- trabajadores ACTIVOS tienen HOY algún ítem "Rechazado" (con RequiereVigencia
-- verdadero) que ahora SÍ cuenta para el retiro automático, separando a los
-- que estarían en ventana de gracia de onboarding (< 21 días de ingreso) de
-- los que no.
-- ============================================================================

WITH ingreso AS (
  SELECT DISTINCT ON (worker_id) worker_id, fecha_ingreso
  FROM workers_periodo_laboral
  WHERE state
  ORDER BY worker_id, fecha_ingreso DESC, workers_periodo_laboral_id DESC
),
rechazados AS (
  SELECT DISTINCT h.worker_id
  FROM ss_hab_trabajador h
  JOIN ss_item_trabajador it ON it.id = h.item_id AND it.requiere_vigencia AND it.activo
  WHERE h.estado = 'Rechazado' AND h.item_id <> 25
)
SELECT
  w.id AS worker_id, p.full_name, w.contrata_casa,
  i.fecha_ingreso,
  CASE WHEN i.fecha_ingreso IS NOT NULL AND (CURRENT_DATE - i.fecha_ingreso) < 21
       THEN 'EN GRACIA (onboarding)' ELSE 'APLICA RETIRO' END AS clasificacion
FROM rechazados r
JOIN workers w ON w.id = r.worker_id AND w.workers_estado_id = (SELECT workers_estado_id FROM workers_estado WHERE codigo = 'ACTIVO')
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN ingreso i ON i.worker_id = w.id
ORDER BY clasificacion, p.full_name;

-- Resumen
SELECT
  CASE WHEN i.fecha_ingreso IS NOT NULL AND (CURRENT_DATE - i.fecha_ingreso) < 21
       THEN 'EN GRACIA (onboarding)' ELSE 'APLICA RETIRO' END AS clasificacion,
  COUNT(*) AS cantidad
FROM (SELECT DISTINCT h.worker_id
      FROM ss_hab_trabajador h
      JOIN ss_item_trabajador it ON it.id = h.item_id AND it.requiere_vigencia AND it.activo
      WHERE h.estado = 'Rechazado' AND h.item_id <> 25) r
JOIN workers w ON w.id = r.worker_id AND w.workers_estado_id = (SELECT workers_estado_id FROM workers_estado WHERE codigo = 'ACTIVO')
LEFT JOIN ingreso i ON i.worker_id = w.id
GROUP BY 1;
