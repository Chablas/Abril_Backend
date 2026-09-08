-- ============================================================================
-- Salidas: capturas obligatorias/opcionales por area + trayectos sin duplicados
-- ============================================================================
-- Dos cosas independientes que viven en el mismo script porque salen del mismo
-- pedido:
--
-- A) Configuracion -> Capturas (seccion nueva). Por cada area de la data
--    maestra (area_scope) se puede decir si sus trabajadores estan obligados a
--    subir capturas de movilidad para poder rendir una salida.
--
--    El flag va en ga_salidas_area_config (la tabla de configuracion de salidas
--    POR AREA, que ya llevaba "filtra_por_proyecto") y NO en area_scope:
--    area_scope es data maestra compartida por todos los modulos y no debe
--    cargar flags de salidas. Ademas, asi el default sale gratis: un area SIN
--    fila queda OBLIGATORIA, que es justo lo pedido -- el codigo que crea areas
--    no se toca y un area nueva nunca queda "sin configurar". Solo marcar un
--    area como OPCIONAL escribe una fila.
--
--    Por eso este script NO inserta filas: al terminar, las 29 areas quedan
--    obligatorias (el estado inicial pedido) sin una sola fila nueva.
--
--    Cada nodo es independiente: no se hereda nada por el arbol. "Unidad de
--    Proyectos" puede estar en opcional e "Ingenieria BIM" (su hija) en
--    obligatorio.
--
-- B) ga_trayecto sin duplicados. La app ya rechazaba el par repetido y la tabla
--    ya tenia UNIQUE (lugar_origen_id, lugar_destino_id) -- pero por ID de
--    ga_lugar, y eso deja pasar el caso real: "OFICINA CENTRAL" existe DOS
--    veces en ga_lugar (una como lugar fijo y otra como proyecto), asi que
--    "Oficina Central -> Bosque Real" se pudo registrar dos veces con ids
--    distintos y en pantalla las dos filas se leen identicas.
--    Se agrega la validacion que falta: la unicidad por LUGAR (el nombre que se
--    muestra), no por id. El sentido inverso (Bosque Real -> Oficina Central)
--    sigue permitido, es otro trayecto.
--
-- Idempotente: se puede correr mas de una vez.
-- ============================================================================

BEGIN;

-- ── A.1. Flag de capturas por area ──────────────────────────────────────────
-- Default true: las capturas son obligatorias salvo que alguien diga lo
-- contrario. NOT NULL para que no exista un tercer estado ("sin definir").

ALTER TABLE ga_salidas_area_config
    ADD COLUMN IF NOT EXISTS capturas_obligatorias boolean NOT NULL DEFAULT true;

COMMENT ON COLUMN ga_salidas_area_config.capturas_obligatorias IS
  'Si true (default), los trabajadores del area deben subir una captura de movilidad por trayecto para poder rendir la salida. En false rinden de frente sin capturas. Un area sin fila en esta tabla se considera true. No se hereda a las subareas: cada nodo de area_scope se configura por separado.';

COMMENT ON TABLE ga_salidas_area_config IS
  'Configuracion del modulo de salidas POR AREA (nodo de area_scope): filtra_por_proyecto y capturas_obligatorias. Desacoplada de la data maestra a proposito (area_scope no lleva flags de salidas). Un area sin fila usa los defaults, por eso un area nueva no necesita que nadie la registre.';

-- ── A.2. Feature de la seccion nueva ────────────────────────────────────────
-- El module_id se DERIVA de una feature hermana ya existente: los ids de module
-- no son los mismos en dev y en prod.

INSERT INTO feature (feature_key, module_id)
SELECT 'gestion-administrativa.config.capturas', f.module_id
FROM feature f
WHERE f.feature_key = 'gestion-administrativa.config.trayectos'
  AND NOT EXISTS (
    SELECT 1 FROM feature WHERE feature_key = 'gestion-administrativa.config.capturas'
  );

-- Mismos roles que hoy configuran Trayectos (ADMINISTRADOR DE SOLICITUD DE
-- SALIDAS): quien administra el catalogo de movilidad es quien define de que
-- areas se exigen capturas. Derivado de role_feature, no por id de rol a mano.
INSERT INTO role_feature (role_id, feature_id)
SELECT rf.role_id, f_new.feature_id
FROM role_feature rf
JOIN feature f_old
  ON f_old.feature_id = rf.feature_id
 AND f_old.feature_key = 'gestion-administrativa.config.trayectos'
CROSS JOIN feature f_new
WHERE f_new.feature_key = 'gestion-administrativa.config.capturas'
  AND NOT EXISTS (
    SELECT 1 FROM role_feature rf2
    WHERE rf2.role_id = rf.role_id
      AND rf2.feature_id = f_new.feature_id
  );

-- ── B.1. Identidad de un lugar ──────────────────────────────────────────────
-- Un lugar es su nombre mostrado, no su id: 'proyecto' resuelve por
-- project.project_description y 'fijo' por ga_lugar.nombre. Un lugar sin nombre
-- resoluble queda unico para si mismo ('#<id>') para no colisionar con nadie.
--
-- Es STABLE y no IMMUTABLE (lee otras tablas), asi que NO se puede usar en un
-- indice unico -- de ahi que la unicidad por lugar se aplique con un trigger.
-- Misma regla que ClaveDe/CargarClavesDeLugarAsync en GaTrayectoRepository.

CREATE OR REPLACE FUNCTION ga_lugar_clave(p_lugar_id integer)
RETURNS text
LANGUAGE sql
STABLE
AS $fn$
    SELECT upper(btrim(coalesce(l.nombre, p.project_description, '#' || l.id::text)))
      FROM ga_lugar l
      LEFT JOIN project p ON p.project_id = l.project_id
     WHERE l.id = p_lugar_id;
$fn$;

COMMENT ON FUNCTION ga_lugar_clave(integer) IS
  'Identidad de un ga_lugar: su nombre mostrado normalizado (mayusculas, sin espacios al borde). Dos filas de ga_lugar con el mismo nombre son el mismo lugar (caso OFICINA CENTRAL, que existe como fijo y como proyecto).';

-- ── B.2. Limpieza de los duplicados que ya existen ──────────────────────────
-- Antes: que hay hoy con el mismo par de LUGARES (deberia listar los dos
-- "Oficina Central -> Bosque Real" en prod/demo).

SELECT ga_lugar_clave(t.lugar_origen_id)  AS origen,
       ga_lugar_clave(t.lugar_destino_id) AS destino,
       count(*)                           AS filas,
       array_agg(t.id ORDER BY t.id)      AS ids,
       array_agg(t.activo ORDER BY t.id)  AS activos
  FROM ga_trayecto t
 GROUP BY 1, 2
HAVING count(*) > 1
 ORDER BY 1, 2;

-- Se DESACTIVAN los repetidos (activo = false), no se borran: ninguna fila se
-- elimina de la BD por auditoria. Se conserva la fila que apunta a los lugares
-- que estan ACTIVOS -- que es la unica que puede volver a matchear una solicitud
-- nueva, porque el desplegable de la solicitud filtra por ga_lugar.activo. Ojo
-- que cual OFICINA CENTRAL esta activa se invierte entre dev y prod, asi que la
-- eleccion se hace por dato y no por id.
-- Empates: gana la que ya esta activa y, si sigue el empate, la mas reciente.
--
-- El UPDATE toca solo la columna activo, asi que no dispara el trigger de B.3
-- (que escucha lugar_origen_id / lugar_destino_id).

WITH claves AS (
    SELECT t.id,
           t.activo,
           ga_lugar_clave(t.lugar_origen_id)  AS k_origen,
           ga_lugar_clave(t.lugar_destino_id) AS k_destino,
           (lo.activo AND ld.activo)          AS lugares_vivos
      FROM ga_trayecto t
      JOIN ga_lugar lo ON lo.id = t.lugar_origen_id
      JOIN ga_lugar ld ON ld.id = t.lugar_destino_id
), rankeados AS (
    SELECT id,
           count(*)     OVER (PARTITION BY k_origen, k_destino) AS cuantos,
           row_number() OVER (PARTITION BY k_origen, k_destino
                              ORDER BY lugares_vivos DESC, activo DESC, id DESC) AS rn
      FROM claves
)
UPDATE ga_trayecto t
   SET activo = false
  FROM rankeados r
 WHERE r.id = t.id
   AND r.cuantos > 1
   AND r.rn > 1
   AND t.activo;

-- ── B.3. Validacion en BD: un solo trayecto por par de lugares ──────────────
-- El UNIQUE por id se conserva (es mas barato y atrapa el caso exacto) y el
-- trigger agrega la unicidad por LUGAR, que es la que el UNIQUE no puede
-- expresar. Ambos idempotentes.

DO $do$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = 'ga_trayecto'::regclass
           AND contype  = 'u'
           AND pg_get_constraintdef(oid) = 'UNIQUE (lugar_origen_id, lugar_destino_id)'
    ) THEN
        ALTER TABLE ga_trayecto
            ADD CONSTRAINT uq_ga_trayecto_origen_destino
            UNIQUE (lugar_origen_id, lugar_destino_id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = 'ga_trayecto'::regclass
           AND contype  = 'c'
           AND pg_get_constraintdef(oid) = 'CHECK ((lugar_origen_id <> lugar_destino_id))'
    ) THEN
        ALTER TABLE ga_trayecto
            ADD CONSTRAINT ck_ga_trayecto_distinct
            CHECK (lugar_origen_id <> lugar_destino_id);
    END IF;
END
$do$;

CREATE OR REPLACE FUNCTION ga_trayecto_sin_duplicados()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_origen  text;
    v_destino text;
    v_otro    integer;
BEGIN
    v_origen  := ga_lugar_clave(NEW.lugar_origen_id);
    v_destino := ga_lugar_clave(NEW.lugar_destino_id);

    -- Un UPDATE que reescribe los lugares con el MISMO lugar de siempre no se
    -- valida: un choque preexistente no puede dejar la fila sin poder editarse.
    IF TG_OP = 'UPDATE'
       AND ga_lugar_clave(OLD.lugar_origen_id)  = v_origen
       AND ga_lugar_clave(OLD.lugar_destino_id) = v_destino
    THEN
        RETURN NEW;
    END IF;

    IF v_origen = v_destino THEN
        RAISE EXCEPTION 'El origen y el destino son el mismo lugar (%)', v_origen
            USING ERRCODE = '23514';
    END IF;

    SELECT t.id INTO v_otro
      FROM ga_trayecto t
     WHERE t.id <> NEW.id
       AND ga_lugar_clave(t.lugar_origen_id)  = v_origen
       AND ga_lugar_clave(t.lugar_destino_id) = v_destino
     LIMIT 1;

    IF v_otro IS NOT NULL THEN
        RAISE EXCEPTION 'Ya existe el trayecto % -> % (ga_trayecto.id = %)',
                        v_origen, v_destino, v_otro
            USING ERRCODE = '23505';
    END IF;

    RETURN NEW;
END;
$fn$;

COMMENT ON FUNCTION ga_trayecto_sin_duplicados() IS
  'Impide dos trayectos con el mismo lugar de origen y el mismo lugar de destino comparando por ga_lugar_clave() (el nombre mostrado) y no por id, porque un mismo lugar puede tener varias filas en ga_lugar. El sentido inverso es otro trayecto y se permite.';

DROP TRIGGER IF EXISTS trg_ga_trayecto_sin_duplicados ON ga_trayecto;

CREATE TRIGGER trg_ga_trayecto_sin_duplicados
    BEFORE INSERT OR UPDATE OF lugar_origen_id, lugar_destino_id ON ga_trayecto
    FOR EACH ROW
    EXECUTE FUNCTION ga_trayecto_sin_duplicados();

COMMIT;

-- ── Verificacion (correr despues del COMMIT) ────────────────────────────────
-- 1) La columna nueva y su default.
--    SELECT column_name, data_type, is_nullable, column_default
--      FROM information_schema.columns
--     WHERE table_name = 'ga_salidas_area_config'
--       AND column_name = 'capturas_obligatorias';
--
-- 2) La feature quedo con sus roles.
--    SELECT f.feature_key, f.module_id, rf.role_id, r.role_description
--      FROM feature f
--      LEFT JOIN role_feature rf ON rf.feature_id = f.feature_id
--      LEFT JOIN role r ON r.role_id = rf.role_id
--     WHERE f.feature_key = 'gestion-administrativa.config.capturas';
--
-- 3) Ya no hay pares de LUGARES repetidos entre trayectos activos.
--    SELECT ga_lugar_clave(lugar_origen_id) AS origen,
--           ga_lugar_clave(lugar_destino_id) AS destino, count(*)
--      FROM ga_trayecto WHERE activo
--     GROUP BY 1, 2 HAVING count(*) > 1;
--
-- 4) Todas las areas arrancan con las capturas obligatorias (0 filas = ok).
--    SELECT c.area_scope_id, ai.area_item_name
--      FROM ga_salidas_area_config c
--      JOIN area_scope s ON s.area_scope_id = c.area_scope_id
--      JOIN area_item ai ON ai.area_item_id = s.area_item_id
--     WHERE c.state AND NOT c.capturas_obligatorias;
