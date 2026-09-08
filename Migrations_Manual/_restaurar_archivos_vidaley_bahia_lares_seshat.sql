-- Continuación de las tandas anteriores. 10 trabajadores (Bahia de Oro,
-- Lares) con archivo Vida Ley recuperable de una póliza anterior al lote
-- contaminado de septiembre 2026.
--
-- Quedan FUERA de este script (no incluidos, requieren decisión aparte):
--   Confirmado que su archivo actual YA ES el correcto (falso positivo de
--   la detección por lote — Seshat no comparte el mismo bug que Aquitania):
--     43091178 GARRO GUERRA LILLIAN PATRICIA — no tocar.
--   Sin ninguna póliza Vida Ley limpia en el historial (no hay nada que
--   restaurar, hay que conseguir el documento de nuevo):
--     72531024 BURGA MORAL YAMILA ALEJANDRA (Neo Inversiones)
--     72410294 IVAN RODRIGO MARQUEZ GONZALES (Lares)
--     72726217 ROJAS CCENCHO LUCERO FIORELLA (Lares)
--   Único archivo alternativo pertenece a OTRA empresa (Salerno/Lares/Thabit
--   para trabajadores de Seshat) — mismo cruce raro ya señalado antes,
--   requiere confirmación manual antes de restaurar. Dado el falso positivo
--   de Garro Guerra, es AÚN MÁS importante confirmar contenido antes de
--   tocar estos:
--     46557961 CANALES GELDRES ALFREDO
--     70274904 HIVET JURIETA MAMANI CONDORI
--     73381575 SILVERA DIAZ SHIRLEY MILAGROS
--     76841365 VICTOR ALEJANDRO COLONIO BARRUETO

BEGIN;

-- PARTE 1: verificar antes de aplicar
WITH correcciones (dni, archivo_correcto) AS (
    VALUES
    ('70982675', 'habilitacion/sctr/20260811_constancia_ABAD_NAUTO_JOHAN.pdf'),           -- ABAD NAUTO JOHAN PIERO
    ('10713842', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),           -- ALAN OCEDA MARIA SONIA
    ('71660883', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),           -- CALDERON VEGA OLENKA ALEXANDRA
    ('71542068', 'habilitacion/sctr/20260710_109593_(6)._marco_collantes.pdf'),           -- COLLANTES ABANTO MARCO ANTONIO
    ('74031156', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),           -- FLORES LOPEZ DAYANNE ALESSANDRA
    ('76325920', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),           -- HINOSTROZA LUIS MAYTE MAEVA
    ('70124175', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),           -- OROZCO CARRILLO MARCOS ENRIQUE
    ('42377772', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),           -- PALMA JIMENEZ MARIA MAGALY
    ('46579206', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),           -- ROJAS LLONTOP JESUS RICARDO
    ('70565134', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf')            -- VERGARAY CARBAJAL CYNTHIA ANEL
    -- GARRO GUERRA LILLIAN PATRICIA (43091178) NO INCLUIDA: confirmado que su
    -- archivo actual (20260804_136735_(1)_seshat_2026.pdf) YA ES el correcto.
    -- No es un caso del bug — no tocar.
)
-- IMPORTANTE: se filtra por vinculación activa (v.fecha_fin IS NULL) porque
-- algunos DNI tienen más de un registro en workers (reingreso/histórico) y
-- sin este filtro el JOIN por person_id trae AMBOS, arriesgando actualizar
-- también un worker viejo/retirado (visto con ABAD NAUTO y FLORES LOPEZ).
SELECT
    corr.dni,
    per.full_name AS nombre,
    w.id AS worker_id,
    w.workers_estado_id,
    h.id AS hab_trabajador_id,
    h.archivo_url AS archivo_actual,
    corr.archivo_correcto,
    (h.archivo_url = corr.archivo_correcto) AS ya_esta_correcto
FROM correcciones corr
JOIN person per ON per.document_identity_code = corr.dni
JOIN workers w ON w.person_id = per.person_id
JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
ORDER BY corr.dni;

-- PARTE 2: descomentar solo después de validar la Parte 1 Y de confirmar
-- (abriendo el PDF) que 20260701_BAHIA130356_(2)._RENOVACION.pdf es
-- realmente Vida Ley y no otro documento — ya nos equivocamos una vez
-- asumiendo esto para Seshat sin abrir el archivo primero.
/*
WITH correcciones (dni, archivo_correcto) AS (
    VALUES
    ('70982675', 'habilitacion/sctr/20260811_constancia_ABAD_NAUTO_JOHAN.pdf'),
    ('10713842', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('71660883', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('71542068', 'habilitacion/sctr/20260710_109593_(6)._marco_collantes.pdf'),
    ('74031156', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('76325920', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('70124175', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('42377772', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('46579206', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'),
    ('70565134', 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf')
),
objetivo AS (
    SELECT h.id AS hab_trabajador_id, corr.archivo_correcto
    FROM correcciones corr
    JOIN person per ON per.document_identity_code = corr.dni
    JOIN workers w ON w.person_id = per.person_id
    JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
    JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
    JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
)
UPDATE ss_hab_trabajador h
SET archivo_url = o.archivo_correcto,
    updated_at = now()
FROM objetivo o
WHERE h.id = o.hab_trabajador_id;
*/

-- Debe reportar 10 filas afectadas. Si coincide: COMMIT. Si no: ROLLBACK.
COMMIT;
