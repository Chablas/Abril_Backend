-- Diagnóstico: staff (app_user) que NO puede subir su capacitación en "Mis Capacitaciones"
-- Causa: SubirMiCapacitacionAsync requiere la cadena app_user -> person (person.user_id) -> worker (worker.person_id)
-- Si falta cualquier eslabón, el backend responde 404 "No se encontró el perfil de trabajador para este usuario."

-- 1) Usuarios activos SIN Person vinculada (person.user_id no apunta a ellos)
SELECT
    u.id            AS user_id,
    u.username,
    u.email
FROM app_user u
LEFT JOIN person p ON p.user_id = u.id
WHERE p.person_id IS NULL
ORDER BY u.id;

-- 2) Usuarios con Person, pero SIN Worker vinculado (worker.person_id no existe)
SELECT
    u.id            AS user_id,
    u.username,
    u.email,
    p.person_id,
    p.full_name
FROM app_user u
JOIN person p ON p.user_id = u.id
LEFT JOIN worker w ON w.person_id = p.person_id
WHERE w.id IS NULL
ORDER BY u.id;

-- 3) Resumen combinado: todo el staff con el estado de su cadena User -> Person -> Worker
SELECT
    u.id                AS user_id,
    u.username,
    u.email,
    p.person_id,
    p.full_name,
    w.id                AS worker_id,
    CASE
        WHEN p.person_id IS NULL THEN 'SIN PERSON (falta person.user_id)'
        WHEN w.id IS NULL THEN 'SIN WORKER (falta worker.person_id)'
        ELSE 'OK'
    END AS estado_cadena
FROM app_user u
LEFT JOIN person p ON p.user_id = u.id
LEFT JOIN worker w ON w.person_id = p.person_id
ORDER BY estado_cadena, u.id;
