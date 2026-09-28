-- ============================================================================
-- Gestión GTH · Reclutamiento — Estado CANCELADO del requerimiento
-- ANTES DE DESPLEGAR EL BACKEND Y EL FRONTEND
-- Fecha: 2026-09-25
--
-- GTH puede cancelar un proceso de selección desde el detalle del requerimiento
-- («Cancelar proceso»), en cualquier fase suya —de Validación GTH al resultado del
-- EMO de ingreso— y hasta que le envía la carta oferta al seleccionado. Cancelar
-- deja el requerimiento en CANCELADO («Cancelado»): estado terminal, nada lo
-- vuelve a mover.
--
-- Se siembra igual que CERRADO_SIN_CUBRIR y RECHAZADO_GG:
--   · active = false: es un estado del requerimiento, no un paso de la línea de
--     tiempo del seguimiento (que solo lista las fases activas).
--   · orden = el de CERRADO (se lee de su fila, no se fija): el proceso terminó.
--     Las transiciones que comparan por orden («si ya llegó más allá, no se
--     mueve») lo tratan así como uno ya terminado. La línea de tiempo lo dibuja
--     en la fase en la que se lo canceló, que sale del historial de estados.
--
-- Correrlo ANTES del deploy es seguro: ningún requerimiento apunta todavía a este
-- estado y el código viejo no lo lee. Sin la fila, el código nuevo responde
-- «No está configurado el estado CANCELADO de reclutamiento» al cancelar.
--
-- Idempotente: se puede correr más de una vez. En dev ya está corrido.
-- ============================================================================

BEGIN;

INSERT INTO gth_estado_requerimiento (codigo, nombre, orden, active, state, descripcion)
SELECT 'CANCELADO',
       'Cancelado',
       c.orden,
       false,
       true,
       'GTH canceló el proceso de selección antes de enviar la carta oferta: el requerimiento no continúa.'
FROM gth_estado_requerimiento c
WHERE c.codigo = 'CERRADO'
  AND c.state
  AND NOT EXISTS (
      SELECT 1 FROM gth_estado_requerimiento e WHERE e.codigo = 'CANCELADO' AND e.state
  );

-- Guarda: si no quedó sembrado (por ejemplo, porque falta la fila de CERRADO), no se
-- confirma nada.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM gth_estado_requerimiento WHERE codigo = 'CANCELADO' AND state) THEN
        RAISE EXCEPTION 'No se sembró CANCELADO: falta la fila vigente de CERRADO en gth_estado_requerimiento. Abortado.';
    END IF;
END $$;

COMMIT;

-- Verificación --------------------------------------------------------------
-- SELECT gth_estado_requerimiento_id, codigo, nombre, orden, active, state
-- FROM gth_estado_requerimiento
-- WHERE codigo IN ('CERRADO', 'CERRADO_SIN_CUBRIR', 'CANCELADO', 'RECHAZADO_GG')
-- ORDER BY orden, gth_estado_requerimiento_id;
