-- Corrige la vigencia de "Vida ley" para 9 trabajadores donde ya se
-- restauró el archivo_url correcto pero la vigencia seguía siendo la de la
-- póliza contaminada de septiembre (2026-09-30). Fecha correcta tomada
-- directamente de ss_sctr_vidaley.vigencia de la póliza cuyo archivo se
-- restauró.

BEGIN;

-- PARTE 1: verificar
WITH corregidos (dni, vigencia_correcta) AS (
    VALUES
    ('10684733', '2027-01-31'::date),  -- PRECIADO ELORRIAGA FERNANDO
    ('70052564', '2027-01-31'::date),  -- BACA ARIAS SCOOT ADERLI
    ('70982675', '2026-08-31'::date),  -- ABAD NAUTO JOHAN PIERO
    ('71386365', '2027-01-31'::date),  -- ALFARO ARRASCUE ALBANIA NAURU
    ('71542068', '2026-08-31'::date),  -- COLLANTES ABANTO MARCO ANTONIO
    ('71950632', '2027-01-31'::date),  -- PALACIOS FHON CARLOS JOSE
    ('71985018', '2027-01-31'::date),  -- GAMARRA VALENCIA ANA LUCIA
    ('73784920', '2027-01-31'::date),  -- SANTA CRUZ VALLEJOS LEYDI ROXANA
    ('76320703', '2027-01-31'::date)   -- COMECA LOJA JOSSELYN ADELITA
)
SELECT
    corr.dni,
    per.full_name AS nombre,
    h.id AS hab_trabajador_id,
    h.archivo_url,
    h.vigencia AS vigencia_actual,
    corr.vigencia_correcta
FROM corregidos corr
JOIN person per ON per.document_identity_code = corr.dni
JOIN workers w ON w.person_id = per.person_id
JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
ORDER BY corr.dni;

-- PARTE 2: descomentar solo después de validar la Parte 1
/*
WITH corregidos (dni, vigencia_correcta) AS (
    VALUES
    ('10684733', '2027-01-31'::date),
    ('70052564', '2027-01-31'::date),
    ('70982675', '2026-08-31'::date),
    ('71386365', '2027-01-31'::date),
    ('71542068', '2026-08-31'::date),
    ('71950632', '2027-01-31'::date),
    ('71985018', '2027-01-31'::date),
    ('73784920', '2027-01-31'::date),
    ('76320703', '2027-01-31'::date)
),
objetivo AS (
    SELECT h.id AS hab_trabajador_id, corr.vigencia_correcta
    FROM corregidos corr
    JOIN person per ON per.document_identity_code = corr.dni
    JOIN workers w ON w.person_id = per.person_id
    JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
    JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
    JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
)
UPDATE ss_hab_trabajador h
SET vigencia = o.vigencia_correcta,
    updated_at = now()
FROM objetivo o
WHERE h.id = o.hab_trabajador_id;
*/

-- Debe reportar 9 filas afectadas. Si coincide: COMMIT. Si no: ROLLBACK.
COMMIT;
