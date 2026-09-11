-- ============================================================================
-- Gestion de Salidas: tope de movilidad (S/ 45)
-- ============================================================================
-- El mismo numero manda en dos momentos distintos del flujo:
--
--   * Al subir capturas y montos -- es el maximo de UN TRAYECTO. Varios
--     trayectos que individualmente no lo pasan si pueden sumar mas de S/ 45
--     entre todos: eso esta permitido y no se corta.
--
--   * Al imprimir la Planilla de Gasto por Movilidad -- es el maximo de UN
--     DIA. Lo que un dia no aguanta sale impreso con la fecha del dia
--     siguiente habil (nunca domingo ni feriado; sabado si). La fecha_salida
--     de la solicitud NO se toca: el traslado es solo del PDF, que es lo que
--     pide RG-42 del requerimiento funcional ("manteniendo trazabilidad del
--     registro original").
--
-- El monto vive en ga_rendicion_config, la fila unica que ya guarda los dias
-- habiles de plazo: es config global de rendiciones, del mismo tipo, y asi no
-- queda un 45 escrito a mano en el codigo. Hoy no hay pantalla que lo edite --
-- se cambia con un UPDATE sobre esta misma tabla.
--
-- Quien lo consume: TopeMovilidad (Shared/Services) al validar cada trayecto,
-- CalendarioNoLaborable al cargarse, e ImputacionMovilidadPlanilla al repartir
-- las fechas del PDF.
--
-- La columna conserva el nombre limite_diario_movilidad con el que nacio: el
-- tope sigue siendo diario en la planilla, que es donde se hace valer contra
-- el dia.
--
-- Idempotente: se puede correr mas de una vez.
-- ============================================================================

SET client_encoding TO 'UTF8';

BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Columna del tope
-- ---------------------------------------------------------------------------
-- NOT NULL con DEFAULT 45.00: la fila que ya existe queda con el tope acordado
-- sin backfill aparte, y una fila nueva nunca nace sin tope.
ALTER TABLE ga_rendicion_config
    ADD COLUMN IF NOT EXISTS limite_diario_movilidad numeric(10,2) NOT NULL DEFAULT 45.00;

-- ---------------------------------------------------------------------------
-- 2) Rango valido
-- ---------------------------------------------------------------------------
-- Mismo criterio que ck_ga_rendicion_config_dias: la base corta el valor
-- absurdo (0, negativo o desmedido) aunque alguien lo toque por SQL a mano.
-- El backend acota al mismo rango antes de comparar, asi que ninguna fila
-- torcida puede abrir el tope de par en par ni dejarlo en cero.
ALTER TABLE ga_rendicion_config
    DROP CONSTRAINT IF EXISTS ck_ga_rendicion_config_limite_diario;

ALTER TABLE ga_rendicion_config
    ADD CONSTRAINT ck_ga_rendicion_config_limite_diario
    CHECK (limite_diario_movilidad > 0 AND limite_diario_movilidad <= 1000);

COMMIT;

-- ============================================================================
-- Verificacion
-- ============================================================================
-- SELECT id, dias_habiles_plazo, limite_diario_movilidad, state
--   FROM ga_rendicion_config;

-- ---------------------------------------------------------------------------
-- Diagnostico previo (opcional, solo SELECT): trayectos que YA se pasan del tope
-- ---------------------------------------------------------------------------
-- El control es hacia adelante: no toca lo ya cargado, y un trayecto que hoy
-- este por encima se puede seguir corrigiendo HACIA ABAJO (lo que se corta es
-- empeorarlo). Esto lista esos casos para saber a cuantos alcanza antes de
-- desplegar. No contempla el tarifario de TI: esos trayectos rinden sin
-- captura, y aca lo que interesa son los montos cargados.
--
-- Ojo: un dia con varios trayectos que entre todos pasen de S/ 45 ya NO es un
-- problema -- la planilla lo reparte sola. Por eso el diagnostico es por
-- trayecto y no por fecha.
--
-- SELECT t.id            AS trayecto_id,
--        s.id            AS solicitud_id,
--        s.codigo,
--        p.full_name     AS trabajador,
--        s.fecha_salida,
--        t.orden + 1     AS trayecto_nro,
--        SUM(c.monto)    AS total_trayecto
--   FROM ga_solicitud_trayecto t
--   JOIN ga_solicitud_salida   s ON s.id = t.solicitud_id
--   JOIN workers w ON w.id = s.worker_id
--   JOIN person  p ON p.person_id = w.person_id
--   JOIN ga_solicitud_captura c ON c.trayecto_id = t.id AND c.state
--  WHERE s.estado_aprobacion_id = 2          -- Aprobado
--  GROUP BY t.id, s.id, s.codigo, p.full_name, s.fecha_salida, t.orden
-- HAVING SUM(c.monto) > 45
--  ORDER BY total_trayecto DESC;
