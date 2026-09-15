-- SOLO LECTURA. Verifica el estado real ahora mismo de los 9 hab_trabajador
-- que se suponía se corrigieron en _corregir_vigencia_restantes.sql, para
-- confirmar si el UPDATE realmente se aplicó o no (caso BACA ARIAS SCOOT
-- sigue mostrando 30/09/2026 en el frontend, cuando debía quedar 2027-01-31).

SELECT
    per.document_identity_code AS dni,
    per.full_name AS nombre,
    h.id AS hab_trabajador_id,
    h.archivo_url,
    h.vigencia,
    h.updated_at
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
WHERE h.id IN (213245, 212283, 218504, 213770, 202265, 213786, 203007, 211759, 213003)
ORDER BY h.id;
