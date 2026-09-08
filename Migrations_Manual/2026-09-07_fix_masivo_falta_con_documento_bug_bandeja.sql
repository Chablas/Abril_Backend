-- ============================================================================
-- FIX MASIVO: ítems "Falta" con documento SÍ subido (archivo_url no nulo) en
-- catálogos que no vencen (requiere_vigencia=false) — huella del bug ya
-- corregido en BandejaRepository.AprobarTrabajadorAsync. Confirmado con
-- diagnóstico: 360 ítems (185 Certijoven + 175 T-Registro), desde julio hasta
-- hoy. Es responsabilidad de Abril (bug de sistema), no de la contratista —
-- no deben poder disparar el retiro automático.
--
-- Devuelve estos ítems a "Aprobado" con la fecha centinela 2040-12-31, igual
-- que el flujo correcto los habría dejado si el bug no hubiera existido.
--
-- Idempotente (el WHERE ya no matchea una vez corregido). Aplicar en dev y
-- prod ANTES de que el retiro automático pase a modo real (2026-09-09).
-- ============================================================================

BEGIN;

UPDATE ss_hab_trabajador h
SET estado = 'Aprobado',
    vigencia = '2040-12-31',
    updated_at = now()
FROM ss_item_trabajador it
WHERE it.id = h.item_id
  AND h.estado = 'Falta'
  AND h.archivo_url IS NOT NULL
  AND it.requiere_vigencia = false;

COMMIT;

-- ============================================================================
-- Verificación (correr después; debe devolver 0 filas)
-- ============================================================================
-- SELECT h.id
-- FROM ss_hab_trabajador h
-- JOIN ss_item_trabajador it ON it.id = h.item_id
-- WHERE h.estado = 'Falta' AND h.archivo_url IS NOT NULL AND it.requiere_vigencia = false;
