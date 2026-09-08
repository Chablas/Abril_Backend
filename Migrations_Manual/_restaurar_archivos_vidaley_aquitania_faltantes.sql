-- Continuación de _restaurar_archivos_vidaley_correctos.sql: estos 10
-- trabajadores de Aquitania quedaron fuera del primer UPDATE porque el
-- archivo Vida Ley correcto anterior no tenía "vida"/"VL" en el nombre
-- (ej. "20260708_constancia._josselyn_comeca.pdf"), así que la heurística
-- por nombre no los detectó. Se identificaron a mano con el historial de
-- ss_sctr_vidaley ya revisado (todas pólizas Vida Ley previas a la de
-- septiembre 2026 que introdujo el archivo SCTR mal cargado).

BEGIN;

-- PARTE 1: verificar antes de aplicar
WITH correcciones (dni, archivo_correcto) AS (
    VALUES
    ('71386365', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),                      -- ALFARO ARRASCUE ALBANIA NAURU
    ('71387814', 'habilitacion/sctr/20260608_aquitania136115_(1)._MANUEL_ASENCIOS.pdf'),        -- ASENCIOS QUISPE MANUEL GILDER
    ('70052564', 'habilitacion/sctr/20260707_136115_(4)._BACA_ARIAS_SCOOT.pdf'),                -- BACA ARIAS SCOOT ADERLI
    ('76320703', 'habilitacion/sctr/20260708_constancia._josselyn_comeca.pdf'),                 -- COMECA LOJA JOSSELYN ADELITA
    ('71985018', 'habilitacion/sctr/20260805_AQUITANIA_136115_(2)._Ana_lucia.pdf'),             -- GAMARRA VALENCIA ANA LUCIA
    ('71950632', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),                       -- PALACIOS FHON CARLOS JOSE
    ('10684733', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),                       -- PRECIADO ELORRIAGA FERNANDO
    ('73784920', 'habilitacion/sctr/20260708_136115_(3)._JUNIO.pdf'),                           -- SANTA CRUZ VALLEJOS LEYDI ROXANA
    ('10723799', 'habilitacion/sctr/20260608_136115_(3)._JUNIO.pdf'),                           -- SCHOLZ LLAQUE SAMANTA ELSA
    ('10684905', 'habilitacion/sctr/20260608_136115_(3)._JUNIO.pdf')                            -- SOLLER CHAVEZ JUAN CARLOS
)
SELECT
    corr.dni,
    per.full_name AS nombre,
    h.id AS hab_trabajador_id,
    h.archivo_url AS archivo_actual,
    corr.archivo_correcto,
    (h.archivo_url = corr.archivo_correcto) AS ya_esta_correcto
FROM correcciones corr
JOIN person per ON per.document_identity_code = corr.dni
JOIN workers w ON w.person_id = per.person_id
JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
ORDER BY corr.dni;

-- Revisa: archivo_actual debe ser "habilitacion/sctr/20260901_AQUITANIA.pdf"
-- en las 10 filas, y ya_esta_correcto debe ser false en todas.

-- PARTE 2: descomentar solo después de validar la Parte 1
/*
WITH correcciones (dni, archivo_correcto) AS (
    VALUES
    ('71386365', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),
    ('71387814', 'habilitacion/sctr/20260608_aquitania136115_(1)._MANUEL_ASENCIOS.pdf'),
    ('70052564', 'habilitacion/sctr/20260707_136115_(4)._BACA_ARIAS_SCOOT.pdf'),
    ('76320703', 'habilitacion/sctr/20260708_constancia._josselyn_comeca.pdf'),
    ('71985018', 'habilitacion/sctr/20260805_AQUITANIA_136115_(2)._Ana_lucia.pdf'),
    ('71950632', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),
    ('10684733', 'habilitacion/sctr/20260708_136115_(5)_julio_2026.pdf'),
    ('73784920', 'habilitacion/sctr/20260708_136115_(3)._JUNIO.pdf'),
    ('10723799', 'habilitacion/sctr/20260608_136115_(3)._JUNIO.pdf'),
    ('10684905', 'habilitacion/sctr/20260608_136115_(3)._JUNIO.pdf')
),
objetivo AS (
    SELECT h.id AS hab_trabajador_id, corr.archivo_correcto
    FROM correcciones corr
    JOIN person per ON per.document_identity_code = corr.dni
    JOIN workers w ON w.person_id = per.person_id
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
