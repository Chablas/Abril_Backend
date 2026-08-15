-- ============================================================================
-- Regularizar al trabajador VASQUEZ SUCUPLE EDUARDO MANUEL
-- DNI 41224003 | app_user 292 (emvasquez@abril.pe) | worker 12235
-- Fecha: 2026-08-14
--
-- Situación actual: dos registros de person describen a la misma persona.
--   * person 10903 "EDUARDO VASQUEZ" -> app_user 292 (emvasquez@abril.pe).
--     Sin DNI, sin ficha en workers. Es la person "de login", creada al dar
--     de alta la cuenta.
--   * person 2476 "VASQUEZ SUCUPLE EDUARDO MANUEL" (DNI 41224003) -> worker
--     12235 (PEÓN / AYUDANTE DE ALMACÉN, Inmobiliaria La Vid S.A.C, ACTIVO).
--     Sin app_user y sin email_corporativo. Es la person real de GTH.
--
-- Efecto del desdoble: el usuario entra a la intranet pero el backend no le
-- encuentra ficha de trabajador, porque toda resolución usuario -> worker va
-- por person.user_id. Salidas, Lecciones y SSOMA lo ven como usuario sin worker.
--
-- Solución: la person buena es la 2476 (tiene DNI, fecha de nacimiento, sexo,
-- número de hijos y la ficha de workers). Se le cuelga el app_user 292, se
-- desactiva la 10903 y se le pone el correo corporativo a la ficha 12235.
--
-- OJO — por qué a la 10903 hay que ponerle user_id = NULL y no solo state=false:
-- person.user_id NO tiene índice único, y hay decenas de queries del tipo
-- `p.UserId == userId` que no filtran por state (LessonRepository,
-- SolicitudSalidaRepository, GestionSalidaRepository, LessonJefeResolver...).
-- Si quedan dos persons apuntando al user 292, esas queries devuelven una fila
-- arbitraria y el bug reaparece de forma intermitente. La fila 10903 no se
-- borra: queda desactivada para auditoría.
--
-- Verificado contra prod antes de escribir esto (2026-08-14):
--   - person 10903 no está referenciada por workers, contributor ni
--     ev_evaluacion_residente (son las únicas 3 FKs que apuntan a person).
--   - ningún otro registro de workers usa emvasquez@abril.pe, así que no
--     choca con el índice parcial ux_workers_email_corporativo_vigente.
--   - person 2476 tiene una sola ficha en workers (12235), con vinculación
--     vigente (worker_vinculaciones 12743, fecha_fin NULL).
--   - app_user 292 ya tiene el rol USUARIO DE ABRIL (user_role role_id 12).
--
-- updated_user_id = 1 (calvarez@abril.pe) queda como autor del cambio.
-- ============================================================================

BEGIN;

-- 1) ESTADO PREVIO
SELECT p.person_id, p.user_id, u.email AS email_login,
       p.document_identity_code AS dni, p.full_name,
       w.id AS worker_id, w.email_corporativo, w.estado,
       p.active, p.state
FROM person p
LEFT JOIN app_user u ON u.user_id = p.user_id
LEFT JOIN workers w  ON w.person_id = p.person_id
WHERE p.person_id IN (2476, 10903)
ORDER BY p.person_id;
-- Esperado: 2476 sin user_id con worker 12235 sin correo, y 10903 con user 292 sin worker.

-- 2) Colgar el app_user 292 de la person real (la que tiene el DNI y la ficha)
UPDATE person
SET user_id           = 292,
    updated_date_time = now(),
    updated_user_id   = 1
WHERE person_id = 2476
  AND document_identity_code = '41224003'
  AND user_id IS NULL;
-- Esperado: UPDATE 1

-- 3) Dar de baja la person duplicada y soltarle el user_id, para que quede
--    exactamente una person apuntando al user 292 (ver nota de la cabecera)
UPDATE person
SET user_id           = NULL,
    active            = false,
    state             = false,
    updated_date_time = now(),
    updated_user_id   = 1
WHERE person_id = 10903
  AND user_id = 292
  AND document_identity_code IS NULL;
-- Esperado: UPDATE 1

-- 4) Completar el correo corporativo de la ficha del trabajador
UPDATE workers
SET email_corporativo = 'emvasquez@abril.pe',
    updated_at        = now()
WHERE id = 12235
  AND person_id = 2476
  AND email_corporativo IS NULL;
-- Esperado: UPDATE 1

-- 5) VERIFICACIÓN — una sola person por el user 292, con DNI, ficha y correo
SELECT u.user_id, u.email AS email_login,
       p.person_id, p.document_identity_code AS dni, p.full_name,
       w.id AS worker_id, w.email_corporativo, w.estado,
       c.nombre AS categoria, pu.nombre AS puesto
FROM app_user u
JOIN person p        ON p.user_id = u.user_id
LEFT JOIN workers w  ON w.person_id = p.person_id
LEFT JOIN categoria c ON c.categoria_id = w.categoria_id
LEFT JOIN puesto pu   ON pu.puesto_id  = w.puesto_id
WHERE u.user_id = 292;
-- Esperado: 1 fila -> person 2476, DNI 41224003, worker 12235,
--           email_corporativo emvasquez@abril.pe, estado ACTIVO.

-- 6) VERIFICACIÓN — no debe quedar ninguna otra person apuntando al user 292
SELECT count(*) AS persons_colgando_del_user_292
FROM person WHERE user_id = 292;
-- Esperado: 1

COMMIT;
