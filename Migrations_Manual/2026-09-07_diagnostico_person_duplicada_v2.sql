-- ============================================================================
-- DIAGNÓSTICO 3 (solo lectura) — búsqueda flexible por palabras del nombre,
-- por si el orden "Nombres Apellidos" vs "Apellidos Nombres" no calzó con el
-- ILIKE exacto del diagnóstico anterior.
-- ============================================================================

-- LUCERO ROJAS
SELECT w.id AS worker_id, p.person_id, p.full_name, p.document_identity_code,
       w.email_corporativo, we.codigo AS estado
FROM workers w
JOIN person p ON p.person_id = w.person_id
JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE p.full_name ILIKE '%LUCERO%' AND p.full_name ILIKE '%ROJAS%';

-- GABRIEL ACOSTA (ya tenemos su DNI: 71405743, buscar por DNI directo)
SELECT w.id AS worker_id, p.person_id, p.full_name, p.document_identity_code,
       w.email_corporativo, we.codigo AS estado
FROM workers w
JOIN person p ON p.person_id = w.person_id
JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE p.document_identity_code = '71405743'
   OR (p.full_name ILIKE '%GABRIEL%' AND p.full_name ILIKE '%ACOSTA%');

-- SANTOS LIZANA
SELECT w.id AS worker_id, p.person_id, p.full_name, p.document_identity_code,
       w.email_corporativo, we.codigo AS estado
FROM workers w
JOIN person p ON p.person_id = w.person_id
JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE p.full_name ILIKE '%SANTOS%' AND p.full_name ILIKE '%LIZANA%';

-- SAMANTA SCHOLZ
SELECT w.id AS worker_id, p.person_id, p.full_name, p.document_identity_code,
       w.email_corporativo, we.codigo AS estado
FROM workers w
JOIN person p ON p.person_id = w.person_id
JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE p.full_name ILIKE '%SAMANTA%' AND p.full_name ILIKE '%SCHOLZ%';
