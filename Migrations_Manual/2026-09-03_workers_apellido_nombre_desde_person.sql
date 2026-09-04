-- ============================================================================
-- workers.apellido_nombre se baja: el nombre del trabajador sale de person
-- ============================================================================
-- El nombre ya vive una sola vez en `person.full_name` (NOT NULL), al que
-- `workers` llega por `person_id`. `workers.apellido_nombre` era la copia vieja:
-- ningun punto del backend la escribe, y las pantallas que todavia la leian lo
-- hacian como fallback de `person.full_name` -- o, peor, ANTES que ella (login
-- del obrero, Auditoria ATS, RAC, inhabilitados SSOMA, Observaciones y
-- Revisiones de Arquitectura Comercial), asi que devolvian vacio o el nombre
-- viejo. Todo eso quedo apuntando a `person.full_name` en el mismo commit que
-- trae este script.
--
-- Verificado contra PROD el 2026-09-03: de 3.627 fichas, UNA sola tiene
-- apellido_nombre con valor (worker 14368 / person 10815) y ese valor es
-- identico al full_name de su person. Ninguna ficha tiene person_id NULL ni
-- huerfano, asi que el DROP no pierde ni un nombre. Nada mas en la base depende
-- de la columna: cero vistas, cero indices, cero constraints, cero funciones.
--
-- El DROP no choca con la regla de auditoria: esa protege FILAS, no columnas que
-- dejaron de capturarse.
--
-- +--------------------------------------------------------------------------+
-- | ORDEN OBLIGATORIO: PRIMERO EL DEPLOY, DESPUES ESTE SCRIPT.               |
-- | EF materializa la entidad Worker con un SELECT de TODAS sus columnas. Si  |
-- | la columna se cae mientras el backend viejo sigue arriba, toda consulta a |
-- | `workers` revienta con 42703 y se cae media aplicacion. El backend nuevo  |
-- | ya no la nombra en ningun lado (ni EF ni SQL crudo de Dapper).            |
-- +--------------------------------------------------------------------------+
--
-- Correr PASO por PASO en pgAdmin. Los dos pasos son idempotentes.
-- ============================================================================


-- ============================================================================
-- PASO 0 - Diagnostico. No modifica nada: correrlo y leer la salida.
-- ============================================================================

-- 0a. Cuanto queda vivo en la columna y cuanto se perderia al bajarla.
--     `riesgo_perdida` es el unico numero que importa: tiene que dar 0.
SELECT count(*)                                                        AS workers_total,
       count(*) FILTER (WHERE btrim(coalesce(w.apellido_nombre,'')) <> '')
                                                                       AS con_apellido_nombre,
       count(*) FILTER (WHERE w.person_id IS NULL)                     AS sin_person_id,
       count(*) FILTER (WHERE w.person_id IS NOT NULL AND p.person_id IS NULL)
                                                                       AS person_id_huerfano,
       count(*) FILTER (WHERE btrim(coalesce(w.apellido_nombre,'')) <> ''
                          AND btrim(coalesce(p.full_name,''))      =  '')
                                                                       AS riesgo_perdida
FROM workers w
LEFT JOIN person p ON p.person_id = w.person_id;

-- 0b. Las fichas que todavia tienen algo en la columna, con su nombre oficial al
--     lado. `iguales = t` significa que la copia vieja no aportaba nada.
SELECT w.id            AS worker_id,
       w.state,
       w.workers_estado_id,
       w.person_id,
       w.apellido_nombre,
       p.full_name,
       p.document_identity_code                                          AS dni,
       upper(btrim(w.apellido_nombre)) = upper(btrim(p.full_name))        AS iguales
FROM workers w
LEFT JOIN person p ON p.person_id = w.person_id
WHERE btrim(coalesce(w.apellido_nombre,'')) <> ''
ORDER BY w.id;


-- ============================================================================
-- PASO 1 - Rescate + guarda + DROP.
--
-- El UPDATE de rescate esta por si entre hoy y el momento de correr esto
-- apareciera una ficha con nombre solo en la columna vieja: en vez de abortar,
-- se lo copia a `person.full_name`, que es donde debe estar. Hoy en prod no
-- toca ninguna fila (0 casos).
--
-- El DO de abajo es la guarda: si despues del rescate quedara UNA sola ficha con
-- nombre que se perderia, aborta la transaccion completa y no se cae nada.
-- ============================================================================
BEGIN;

-- 1a. Rescate: el nombre que solo exista en la columna vieja pasa a person.
UPDATE person p
SET    full_name = btrim(w.apellido_nombre)
FROM   workers w
WHERE  w.person_id = p.person_id
  AND  btrim(coalesce(w.apellido_nombre,'')) <> ''
  AND  btrim(coalesce(p.full_name,''))       =  '';

-- 1b. Guarda: nada se puede perder.
DO $$
DECLARE
    v_riesgo bigint;
BEGIN
    SELECT count(*)
      INTO v_riesgo
      FROM workers w
      LEFT JOIN person p ON p.person_id = w.person_id
     WHERE btrim(coalesce(w.apellido_nombre,'')) <> ''
       AND btrim(coalesce(p.full_name,''))       =  '';

    IF v_riesgo > 0 THEN
        RAISE EXCEPTION
            'ABORTADO: % ficha(s) de workers tienen nombre solo en apellido_nombre '
            'y su person no lo tiene. Revisar el PASO 0b antes de bajar la columna.',
            v_riesgo;
    END IF;
END $$;

-- 1c. El DROP.
ALTER TABLE workers DROP COLUMN IF EXISTS apellido_nombre;

COMMIT;


-- ============================================================================
-- Verificacion (despues del COMMIT): tiene que devolver 0 filas.
-- ============================================================================
SELECT column_name
FROM   information_schema.columns
WHERE  table_schema = 'public'
  AND  table_name   = 'workers'
  AND  column_name  = 'apellido_nombre';

-- Ojo: `apellido_nombre` sigue existiendo -- y se queda -- en otras dos tablas
-- que no tienen nada que ver con esta migracion:
--   ss_medicos_ocupacionales.apellido_nombre  (medicos, no son workers)
--   ss_trabajador_restringido.apellido_nombre (lista negra por DNI/nombre libre)
