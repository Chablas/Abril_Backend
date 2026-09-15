-- ============================================================================
-- DIAGNÓSTICO 2 (solo lectura) — ¿existe una ficha real de trabajador para
-- estas personas bajo un person_id DISTINTO (Person duplicada)?
--
-- Hipótesis: sus cuentas (Lucero Rojas, Gabriel Acosta, Samanta Scholz,
-- Santos Lizana) se crearon con el flujo VIEJO (antes del fix que exige
-- matchear workers.email_corporativo) y quedó una Person "huérfana" sin
-- worker. Si su DNI real aparece en OTRA fila de person con SU PROPIO
-- worker, el fix es re-vincular app_user.person_id -> ese person_id.
-- ============================================================================

SELECT u.email AS login_email, u.user_id,
       p_huerfana.person_id AS person_huerfana_id, p_huerfana.document_identity_code AS dni_huerfana,
       p_real.person_id AS person_con_worker_id, p_real.full_name, p_real.document_identity_code AS dni_real,
       w.id AS worker_id, w.email_corporativo, we.codigo AS estado
FROM app_user u
JOIN person p_huerfana ON p_huerfana.user_id = u.user_id
LEFT JOIN person p_real
       ON p_real.person_id <> p_huerfana.person_id
      AND (
            (p_huerfana.document_identity_code IS NOT NULL
             AND p_real.document_identity_code = p_huerfana.document_identity_code)
         OR p_real.full_name ILIKE p_huerfana.full_name
          )
LEFT JOIN workers w ON w.person_id = p_real.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE u.email IN ('lfrojas@abril.pe', 'gacosta@abril.pe', 'sscholz@abril.pe', 'slizana@abril.pe')
ORDER BY u.email, we.codigo;

-- Si esto no devuelve nada (ningún p_real), es porque el match por nombre/DNI
-- no calzó exacto o de verdad no existe ficha en workers para ellos todavía
-- (nunca se les dio de alta como trabajador) — en ese caso el fix sería
-- distinto: crear la ficha, no re-vincular.
