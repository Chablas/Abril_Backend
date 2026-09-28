-- SOLO CONSULTA (no modifica nada). Usa solo 'batalla' (sin tilde) para evitar el problema de
-- acentos del intento anterior ("Junín" con tilde no matcheaba "junin" sin tilde).

-- 1) Todos los contactos de la contratista con su etiqueta
SELECT
    c.contributor_id,
    c.contributor_name,
    ce.contractor_email,
    pt.description AS tipo_contacto,
    ce.active AS contacto_activo,
    ce.state  AS contacto_state
FROM contractor_email ce
JOIN contractor ct ON ct.contractor_id = ce.contractor_id
JOIN contributor c ON c.contributor_id = ct.contributor_id
LEFT JOIN contractor_person_type pt ON pt.contractor_person_type_id = ce.contractor_person_type_id
WHERE c.contributor_name ILIKE '%batalla%'
ORDER BY pt.description NULLS LAST;

-- 2) email_administrador asignado en Gestión de Responsables (si existe)
SELECT contributor_id, contributor_name, email_administrador
FROM contributor
WHERE contributor_name ILIKE '%batalla%';

-- 3) Usuario titular del portal (login) de esa contratista
SELECT c.contributor_name, au.email AS email_titular_portal
FROM contractor_user cu
JOIN contractor ct ON ct.contractor_id = cu.contractor_id
JOIN contributor c ON c.contributor_id = ct.contributor_id
JOIN app_user au ON au.user_id = cu.user_id
WHERE c.contributor_name ILIKE '%batalla%'
  AND cu.active = true AND cu.state = true;

-- 4) Lo que resolvería la NUEVA prioridad, en un solo query (contacto Gerente/Administrador/
--    Representante Legal > email_administrador > usuario titular del portal)
SELECT COALESCE(
    (SELECT ce.contractor_email
     FROM contractor_email ce
     JOIN contractor ct ON ct.contractor_id = ce.contractor_id
     JOIN contributor c ON c.contributor_id = ct.contributor_id
     LEFT JOIN contractor_person_type pt ON pt.contractor_person_type_id = ce.contractor_person_type_id
     WHERE c.contributor_name ILIKE '%batalla%'
       AND ce.active = true AND ce.state = true AND ct.active = true
       AND pt.description ILIKE ANY (ARRAY['%geren%', '%administrad%', '%representante legal%'])
     ORDER BY ce.created_date_time
     LIMIT 1),
    (SELECT email_administrador FROM contributor WHERE contributor_name ILIKE '%batalla%'),
    (SELECT au.email
     FROM contractor_user cu
     JOIN contractor ct ON ct.contractor_id = cu.contractor_id
     JOIN contributor c ON c.contributor_id = ct.contributor_id
     JOIN app_user au ON au.user_id = cu.user_id
     WHERE c.contributor_name ILIKE '%batalla%' AND cu.active = true AND cu.state = true
     LIMIT 1)
) AS correo_resuelto_nueva_prioridad;
