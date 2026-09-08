-- Corrige la vigencia de "Vida ley" para los 8 trabajadores de Bahia de Oro
-- cuyo archivo se restauró a BAHIA130356_(2)._RENOVACION.pdf. Confirmado
-- abriendo el PDF: "SEGURO DE VIDA EN GRUPO OBLIGATORIO... póliza de Vida
-- Ley N° 130356 por la vigencia 01 de Julio del 2026 al 30 de Junio del
-- 2027" -> vigencia correcta = 2027-06-30 (no 2026-09-30, que era la fecha
-- de la póliza contaminada de septiembre).

BEGIN;

-- PARTE 1: verificar
WITH corregidos (dni) AS (
    VALUES ('10713842'), ('71660883'), ('74031156'), ('76325920'),
           ('70124175'), ('42377772'), ('46579206'), ('70565134')
)
SELECT
    corr.dni,
    per.full_name AS nombre,
    h.id AS hab_trabajador_id,
    h.archivo_url,
    h.vigencia AS vigencia_actual
FROM corregidos corr
JOIN person per ON per.document_identity_code = corr.dni
JOIN workers w ON w.person_id = per.person_id
JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
ORDER BY corr.dni;

-- Confirma que archivo_url = habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf
-- en las 8 filas antes de seguir.

-- PARTE 2: descomentar solo después de validar la Parte 1
/*
WITH corregidos (dni) AS (
    VALUES ('10713842'), ('71660883'), ('74031156'), ('76325920'),
           ('70124175'), ('42377772'), ('46579206'), ('70565134')
),
objetivo AS (
    SELECT h.id AS hab_trabajador_id
    FROM corregidos corr
    JOIN person per ON per.document_identity_code = corr.dni
    JOIN workers w ON w.person_id = per.person_id
    JOIN worker_vinculaciones vinc ON vinc.worker_id = w.id AND vinc.fecha_fin IS NULL
    JOIN ss_item_trabajador it ON it.es_sctr_vidaley = true AND it.activo = true AND it.nombre ILIKE '%Vida%'
    JOIN ss_hab_trabajador h ON h.worker_id = w.id AND h.item_id = it.id
    WHERE h.archivo_url = 'habilitacion/sctr/20260701_BAHIA130356_(2)._RENOVACION.pdf'
)
UPDATE ss_hab_trabajador h
SET vigencia = '2027-06-30',
    updated_at = now()
FROM objetivo o
WHERE h.id = o.hab_trabajador_id;
*/

-- Debe reportar 8 filas afectadas. Si coincide: COMMIT. Si no: ROLLBACK.
COMMIT;
