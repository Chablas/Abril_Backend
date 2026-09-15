-- ============================================================================
-- FIX: re-vincular app_user -> person REAL (la que tiene ficha en workers),
-- en vez de la person huérfana que quedó del flujo viejo de Microsoft SSO
-- (antes del fix que exige matchear workers.email_corporativo en el primer
-- login). Casos confirmados: lfrojas, slizana, sscholz (ver diagnóstico).
--
-- No borra nada: solo mueve user_id de la person huérfana a la person real,
-- y limpia el user_id de la huérfana para que quede consistente.
--
-- Idempotente (WHERE explícito por person_id, no rompe si se corre 2 veces).
-- Aplicar en dev y prod.
-- ============================================================================

BEGIN;

-- Lucero Rojas: user_id 554, huérfana 11797 -> real 11581
UPDATE person SET user_id = NULL             WHERE person_id = 11797 AND user_id = 554;
UPDATE person SET user_id = 554              WHERE person_id = 11581;

-- Santos Lizana: user_id 476, huérfana 11329 -> real 8293
UPDATE person SET user_id = NULL             WHERE person_id = 11329 AND user_id = 476;
UPDATE person SET user_id = 476              WHERE person_id = 8293;

-- Samanta Scholz: user_id 532, huérfana 11627 -> real 5426
UPDATE person SET user_id = NULL             WHERE person_id = 11627 AND user_id = 532;
UPDATE person SET user_id = 532              WHERE person_id = 5426;

COMMIT;

-- ============================================================================
-- Verificación (correr después)
-- ============================================================================
-- SELECT u.email, p.person_id, p.full_name, w.id AS worker_id, w.email_corporativo
-- FROM app_user u
-- JOIN person p ON p.user_id = u.user_id
-- LEFT JOIN workers w ON w.person_id = p.person_id
-- WHERE u.user_id IN (554, 476, 532);
-- Esperado: cada fila con worker_id NO nulo.
