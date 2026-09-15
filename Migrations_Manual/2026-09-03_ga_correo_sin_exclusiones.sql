-- ============================================================================
-- Salidas: se elimina la lista "nunca se enviara a" de la configuracion de correos
-- ============================================================================
-- La pantalla de Configuracion -> Correos tenia dos listas por cada correo:
-- "se enviara a" y "nunca se enviara a" (ga_correo_regla.es_exclusion). La segunda
-- se da de baja: nunca quedaba registro de POR QUE alguien estaba excluido, y la
-- misma exclusion se logra apagando o quitando su fila de la lista de envio.
--
-- La data de las exclusiones se pierde a proposito (pedido explicito). Se borran de
-- verdad y no con state = false: dejarlas como soft delete solo escondera filas que
-- ya no significan nada, porque la columna que las distingue tambien se va.
--
-- Ademas, todos los correos pasan a ser apagables y todos sus destinatarios
-- principales tambien: la pantalla nueva muestra un interruptor por destinatario,
-- incluido el revisor.
--
-- ⚠ ORDEN DE EJECUCION
--   PARTE 1  → ANTES o DESPUES del deploy, da igual (no cambia el esquema).
--   deploy
--   PARTE 2  → SOLO DESPUES del deploy (bota la columna que el backend viejo lee).
--
-- Idempotente: las dos partes se pueden correr mas de una vez.
-- ============================================================================


-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║ PARTE 1 — se puede correr antes del deploy                               ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

BEGIN;

-- ── 1. Baja definitiva de las exclusiones ───────────────────────────────────
-- Antes de borrar, se deja constancia en el log de cuantas se van y a quien
-- apuntaban: es lo unico que va a quedar de ellas.

DO $$
DECLARE
    detalle text;
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_name = 'ga_correo_regla' AND column_name = 'es_exclusion') THEN

        SELECT string_agg(format('%s -> %s', e.codigo,
                                 COALESCE(r.correo, 'worker ' || r.worker_id::text,
                                          'area ' || r.area_scope_id::text)), ', ')
          INTO detalle
          FROM ga_correo_regla r
          JOIN ga_correo_evento e ON e.id = r.evento_id
         WHERE r.es_exclusion;

        RAISE NOTICE 'Exclusiones que se eliminan: %', COALESCE(detalle, '(ninguna)');

        DELETE FROM ga_correo_regla WHERE es_exclusion;
    END IF;
END $$;

-- ── 2. Todo correo y todo principal pasan a ser apagables ───────────────────
-- La pantalla nueva dibuja un interruptor por fila, incluida la del destinatario
-- principal. Con estos flags en false el interruptor no se muestra y la fila queda
-- muerta, que es justo lo contrario de lo que se pidio.

UPDATE ga_correo_evento
   SET permite_desactivar_envio     = true,
       permite_desactivar_principal = true,
       updated_at                   = now()
 WHERE state
   AND (NOT permite_desactivar_envio OR NOT permite_desactivar_principal);

COMMIT;

-- ── Verificacion de la PARTE 1 ──────────────────────────────────────────────
-- SELECT codigo, active, permite_desactivar_envio, permite_desactivar_principal,
--        destinatario_principal_activo, destinatario_principal_nombre
--   FROM ga_correo_evento WHERE state ORDER BY orden;


-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║ PARTE 2 — SOLO DESPUES DE DESPLEGAR EL BACKEND                           ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
-- El backend que hoy corre en produccion todavia lee es_exclusion al resolver los
-- destinatarios de cada correo. Botando la columna antes del deploy, TODOS los
-- correos de salidas se caen (el resolver entra al catch y manda solo a la base).

/*
BEGIN;

ALTER TABLE ga_correo_regla DROP COLUMN IF EXISTS es_exclusion;

COMMIT;
*/
