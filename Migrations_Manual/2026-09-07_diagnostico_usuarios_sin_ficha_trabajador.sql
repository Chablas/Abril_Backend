-- ============================================================================
-- DIAGNÓSTICO (solo lectura, no modifica nada) — usuarios sin ficha de
-- trabajador vinculada ("Tu usuario no está vinculado a una ficha de
-- trabajador."), caso reportado: Lucero Rojas.
--
-- Mecanismo (ver MicrosoftLoginService.cs / WorkerSearchRepository.cs):
--   1) Primer login SSO: se busca en workers.email_corporativo un match
--      EXACTO (lower, sin trim) contra el correo de Microsoft.
--   2) Si matchea, se crea app_user y se setea person.user_id = ese person_id
--      (el mismo person_id que usa esa ficha de workers).
--   3) Pantallas como Inspección/Indicadores Proactivos buscan la ficha vía
--      workers.person_id = person.person_id WHERE person.user_id = <userId>.
--
-- Si esto falla, es casi siempre por una de estas causas (que este script
-- detecta, cada una en su propia consulta):
--   A) El email_corporativo tiene espacios en blanco o mayúsculas raras que
--      SÍ igualan en Graph pero no calzan con lo guardado (trim faltante).
--   B) El worker.email_corporativo quedó vacío/NULL o desactualizado después
--      del primer login (p. ej. cambió de obra y se le reescribió el correo).
--   C) person.user_id apunta a una Person que no es la de la ficha ACTIVA
--      vigente de esa persona (caso reingreso: dos fichas comparten person,
--      pero alguna quedó sin person_id bien puesto).
-- ============================================================================

-- 1) Caso puntual: Lucero Rojas
SELECT u.user_id, u.email AS user_email, u.active AS user_activo,
       p.person_id, p.full_name, p.document_identity_code,
       w.id AS worker_id, w.email_corporativo, w.workers_estado_id, we.codigo AS estado_codigo
FROM app_user u
LEFT JOIN person p ON p.user_id = u.user_id
LEFT JOIN workers w ON w.person_id = p.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE p.full_name ILIKE '%lucero%rojas%' OR u.email ILIKE '%lucero%';

-- 2) Universal: usuarios con person vinculada pero SIN ninguna fila en workers
--    para esa person (el bug exacto del mensaje reportado).
SELECT u.user_id, u.email, p.person_id, p.full_name, p.document_identity_code
FROM app_user u
JOIN person p ON p.user_id = u.user_id
WHERE u.active AND u.state
  AND NOT EXISTS (SELECT 1 FROM workers w WHERE w.person_id = p.person_id)
ORDER BY p.full_name;

-- 3) Universal: workers activos cuyo email_corporativo tiene espacios en
--    blanco (candidatos a fallar el match en el PRIMER login, causa A).
SELECT w.id, p.full_name, w.email_corporativo,
       '[' || w.email_corporativo || ']' AS con_marcas_visibles
FROM workers w
JOIN person p ON p.person_id = w.person_id
WHERE w.workers_estado_id IN (SELECT workers_estado_id FROM workers_estado WHERE codigo = 'ACTIVO')
  AND w.email_corporativo IS NOT NULL
  AND w.email_corporativo <> btrim(w.email_corporativo);

-- 4) Universal: personas con MÁS de una ficha (reingreso) donde la ficha
--    ACTIVA no es la que tiene el email_corporativo usado para el login
--    (causa C) — compara contra el correo real de su app_user.
SELECT p.person_id, p.full_name, u.email AS login_email,
       w.id AS worker_id, w.email_corporativo, we.codigo AS estado
FROM person p
JOIN app_user u ON u.user_id = p.user_id
JOIN workers w ON w.person_id = p.person_id
JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
WHERE p.person_id IN (
    SELECT person_id FROM workers GROUP BY person_id HAVING COUNT(*) > 1
)
ORDER BY p.person_id, we.codigo;
