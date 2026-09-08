-- ============================================================================
-- Borrado de correos inválidos que rebotan (mal escritos / placeholders),
-- causando que las notificaciones (vencimientos, retiro automático,
-- evaluaciones EMO, etc.) se "envíen" pero nunca lleguen a destino.
--
-- Fuentes reales que usa RetiroAutomaticoService para armar destinatarios:
--   - contributor.email_administrador   (correo del admin de la empresa contratista)
--   - workers.email_corporativo         (coordinador administrativo / jefe SSOMA)
-- ss_contratista_usuario NO tiene columna de email (se descarta).
-- ============================================================================

-- Si venías de un error anterior en esta misma sesión de pgAdmin, primero:
-- ROLLBACK;

-- 1) Diagnóstico ILIKE (busca coincidencia parcial/insensible a mayúsculas,
--    para no perder casos por un espacio, mayúscula, o un punto de más al final)

SELECT contributor_id, contributor_name, email_administrador
FROM contributor
WHERE email_administrador ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

SELECT id, email_corporativo
FROM workers
WHERE email_corporativo ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

SELECT person_id, full_name, email
FROM person
WHERE email ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

-- Correos de contacto de contratistas (módulo Costos, tabla separada de contributor)
SELECT contractor_email_id, contractor_id, contractor_email
FROM contractor_email
WHERE contractor_email ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

SELECT staff_project_email_id, project_id, email
FROM staff_project_email
WHERE email ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

-- Destinatarios configurables (no ligados a una persona/trabajador)
SELECT id, codigo, email, nombre
FROM ss_emo_correo_destinatario
WHERE email ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

SELECT gth_correo_destinatario_id, codigo, email, nombre
FROM gth_correo_destinatario
WHERE email ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

SELECT id, clinica_id, nombre, email
FROM ss_clinica_emails
WHERE email ILIKE ANY (ARRAY[
    '%jparin%', '%gygestructurasymontaje%', '%notiene%', '%jaqueli457%',
    '%adrianahuaman456%', '%famado@abril%', '%jperez@lumbreras%', '%respaldoyperalta%'
]);

-- 2) Si el diagnóstico se ve bien, correr esto:

UPDATE contributor
SET email_administrador = NULL
WHERE email_administrador IN (
    'jpariño@gmail.com', 'jparino@gmail.com', 'gygestructurasymontaje@gmail.com',
    'notiene@gmail.com', 'jaqueli457@gmail.com', 'adrianahuaman456@gmail.com',
    'famado@abril.pe.', 'famado@abril.pe', 'jperez@lumbreras.pe', 'respaldoyperalta@abril.pe'
);

UPDATE workers
SET email_corporativo = NULL
WHERE email_corporativo IN (
    'jpariño@gmail.com', 'jparino@gmail.com', 'gygestructurasymontaje@gmail.com',
    'notiene@gmail.com', 'jaqueli457@gmail.com', 'adrianahuaman456@gmail.com',
    'famado@abril.pe.', 'famado@abril.pe', 'jperez@lumbreras.pe', 'respaldoyperalta@abril.pe'
);

UPDATE person
SET email = NULL
WHERE email IN (
    'jpariño@gmail.com', 'jparino@gmail.com', 'gygestructurasymontaje@gmail.com',
    'notiene@gmail.com', 'jaqueli457@gmail.com', 'adrianahuaman456@gmail.com',
    'famado@abril.pe.', 'famado@abril.pe', 'jperez@lumbreras.pe', 'respaldoyperalta@abril.pe'
);
