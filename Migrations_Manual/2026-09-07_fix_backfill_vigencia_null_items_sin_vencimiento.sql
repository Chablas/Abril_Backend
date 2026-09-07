-- ============================================================================
-- FIX (backfill) — corrige los registros ss_hab_trabajador que quedaron
-- "Aprobado" con vigencia NULL por el bug de BandejaRepository.AprobarTrabajadorAsync
-- (ya corregido en código): al aprobar sin fecha, preservaba el null en vez de
-- calcular la vigencia sintética 2040-12-31 para ítems con requiere_vigencia=false.
--
-- Solo toca ítems marcados como "no vence" en el catálogo (requiere_vigencia=false).
-- Los que sí requieren vigencia real (true) NO se tocan aquí — esos habría que
-- revisarlos caso por caso porque de verdad les falta una fecha real.
-- ============================================================================

UPDATE ss_hab_trabajador h
SET vigencia = '2040-12-31 00:00:00+00',
    updated_at = now()
FROM ss_item_trabajador it
WHERE it.id = h.item_id
  AND it.requiere_vigencia = false
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL;

-- Verificación (correr después): debe devolver 0 filas.
-- SELECT h.id, it.nombre, h.estado, h.vigencia
-- FROM ss_hab_trabajador h
-- JOIN ss_item_trabajador it ON it.id = h.item_id
-- WHERE it.requiere_vigencia = false AND h.estado = 'Aprobado' AND h.vigencia IS NULL;
