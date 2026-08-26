-- =====================================================================
-- Fusión de personas duplicadas — CARLOS JEAN PIERRE CASTAÑEDA HERRERA
-- Ejecutar en PRODUCCIÓN. Archivo en UTF-8 (contiene Ñ).
--
-- Sobrevive : person 859   → ficha GTH con DNI, datos completos y workers 13994
-- Se fusiona : person 11609 → "person mínima" creada por el alta de usuario,
--                             su único aporte es el vínculo user_id = 526
--
-- Verificado antes de escribir esto: ninguna de las 7 columnas person_id del
-- esquema (workers, gth_onboarding, gth_postulante_formulario,
-- workers_ficha_fusionada, contributor.legal_representative_person_id,
-- ev_evaluacion_residente.evaluador_person_id) referencia a la 11609.
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- 1) La duplicada suelta el usuario y se da de baja.
--    Va PRIMERO: person.user_id no tiene UNIQUE, así que dejar las dos
--    apuntando al 526 aunque sea un instante es justo el estado que
--    vuelve no determinista el FirstOrDefault de ResolverWorkerIdAsync.
--    No se borra la fila: state = false es la baja lógica (auditoría).
-- ---------------------------------------------------------------------
UPDATE person
SET    user_id           = NULL,
       active            = false,
       state             = false,
       updated_date_time = now()
WHERE  person_id = 11609
  AND  user_id   = 526;   -- guarda: si no está como esperamos, no toca nada

-- ---------------------------------------------------------------------
-- 2) La ficha buena se queda con el usuario y con los nombres desglosados.
--    Los COALESCE(NULLIF(...)) solo rellenan lo que esté vacío: si mañana
--    GTH ya cargó esos campos, esta sentencia no los pisa.
--
--    OJO — desglose asumido a partir de full_name 'CASTAÑEDA HERRERA
--    CARLOS JEAN PIERRE' (paterno / materno / nombres). La 11609 traía
--    'CASTAÑEDA HERRERA' junto en first_last_name (viene del display name
--    de Microsoft, sin separar); acá se separa como corresponde.
-- ---------------------------------------------------------------------
UPDATE person
SET    user_id           = 526,
       first_names       = COALESCE(NULLIF(TRIM(first_names),      ''), 'CARLOS JEAN PIERRE'),
       first_last_name   = COALESCE(NULLIF(TRIM(first_last_name),  ''), 'CASTAÑEDA'),
       second_last_name  = COALESCE(NULLIF(TRIM(second_last_name), ''), 'HERRERA'),
       updated_date_time = now()
WHERE  person_id = 859;

COMMIT;


-- =====================================================================
-- VERIFICACIÓN (correr después del COMMIT)
-- Esperado: UNA sola fila → person 859, user 526, worker 13994.
-- Es exactamente la cadena que recorre MiSaludRepository.ResolverWorkerIdAsync.
-- =====================================================================
SELECT p.person_id,
       p.document_identity_code AS dni,
       p.full_name,
       p.first_last_name,
       p.second_last_name,
       p.first_names,
       p.state,
       u.user_id,
       u.email,
       w.id AS worker_id,
       w.state AS worker_state
FROM   person p
JOIN   app_user u ON u.user_id = p.user_id
LEFT   JOIN workers w ON w.person_id = p.person_id AND w.state
WHERE  p.user_id = 526;

-- Y que la duplicada quedó fuera de circulación (state = f, user_id nulo):
SELECT person_id, full_name, user_id, active, state
FROM   person
WHERE  person_id = 11609;
