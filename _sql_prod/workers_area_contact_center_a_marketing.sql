-- =============================================================================
-- CORRECCION DE DATA (prod): workers.area_scope_id  63 (Ventas) -> 75 (Marketing)
-- para las fichas vivas con puesto 225 OPERADOR DE CONTACT CENTER.
--
-- Motivo: son 8 de las fichas que salian con coincide = false comparando
-- workers.area_scope_id contra puesto.area_destino_scope_id. El puesto 225 tiene
-- destino 75 (Gerencia de Marketing > Marketing) y la ficha decia 63
-- (Gerencia de Administracion > Ventas). Se alinea la ficha al puesto.
--
-- Verificado en PROD el 2026-09-02: 8 fichas candidatas
--   12092 SCHOLZ LLAQUE SAMANTA ELSA           ACTIVO
--   12963 LAZARO PONCE ELSA ELISA              ACTIVO
--   13262 GARCIA LADCANI DEBORAH               ACTIVO
--   13410 ANTON QUIROZ ANGELO JESUS            ACTIVO
--   13450 DIAZ REYES MAURICIO JOSE             ACTIVO
--   13526 OLIVA VERA TUDELA DANIELA ALEXANDRA  ACTIVO
--   13856 SONCO MATTA MARICIELO CRISTEL        ACTIVO
--   14193 ELIO ENRIQUE RAMIREZ YUPANQUI        RETIRADO
--
-- La guarda del paso 0 aborta la transaccion completa si el escenario cambio
-- (otro destino en el puesto, u otra cantidad de fichas): no toca nada.
-- Sin tildes a proposito para que corra igual por -c inline en PowerShell.
-- =============================================================================

BEGIN;

-- 0) GUARDA: el escenario tiene que ser exactamente el verificado.
DO $$
DECLARE
    v_destino    int;
    v_candidatas int;
BEGIN
    SELECT area_destino_scope_id
      INTO v_destino
      FROM puesto
     WHERE puesto_id = 225
       AND nombre    = 'OPERADOR DE CONTACT CENTER'
       AND state;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'No existe el puesto 225 OPERADOR DE CONTACT CENTER vivo. Abortado.';
    END IF;

    IF v_destino IS DISTINCT FROM 75 THEN
        RAISE EXCEPTION 'El puesto 225 tiene area de destino % y se esperaba 75. Abortado.', v_destino;
    END IF;

    SELECT count(*)
      INTO v_candidatas
      FROM workers
     WHERE state
       AND puesto_id     = 225
       AND area_scope_id = 63;

    IF v_candidatas <> 8 THEN
        RAISE EXCEPTION 'Se esperaban 8 fichas con puesto 225 y area 63, hay %. Revisar antes de actualizar.', v_candidatas;
    END IF;
END $$;

-- 1) UPDATE
UPDATE workers
   SET area_scope_id = 75,
       updated_at    = now()
 WHERE state
   AND puesto_id     = 225
   AND area_scope_id = 63;
-- Esperado: UPDATE 8

-- 2) VERIFICACION: las 8 fichas deben quedar en 75 y con coincide = true.
SELECT
    w.id                                        AS ficha_id,
    pe.full_name                                AS trabajador,
    w.area_scope_id                             AS area_ficha_id,
    p.area_destino_scope_id                     AS area_puesto_id,
    (w.area_scope_id = p.area_destino_scope_id) AS coincide,
    we.codigo                                   AS estado
FROM workers w
LEFT JOIN person         pe ON pe.person_id = w.person_id
JOIN      puesto         p  ON p.puesto_id  = w.puesto_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE w.state
  AND w.puesto_id = 225
ORDER BY we.codigo, pe.full_name;

COMMIT;

-- =============================================================================
-- QUEDA FUERA a proposito: la ficha 13271 (SEBASTIAN ALBERTO ALCANTARA GARCIA,
-- RETIRADO) tiene el mismo patron 63 -> 75 pero con el puesto 61 ASISTENTE
-- DIGITAL. Para incluirla, cambiar los tres `puesto_id = 225` por
-- `puesto_id IN (61, 225)` y el 8 de la guarda por 9.
-- =============================================================================
