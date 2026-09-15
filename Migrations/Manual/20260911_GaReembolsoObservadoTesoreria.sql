-- ============================================================================
-- Gestión Administrativa — Reembolsos: Tesorería puede OBSERVAR el consolidado
-- antes de autorizar el pago (RG-49 del requerimiento funcional de salidas).
--
-- Qué cambia y por qué
--
-- Hasta ahora Tesorería solo tenía dos botones —confirmar la revisión y pagar—
-- y ningún camino de vuelta: si al revisar la documentación encontraba algo mal,
-- no había forma de devolverla. RG-49 lo pide explícito:
--
--     "Tesorería podrá observar un Consolidado antes de autorizar el pago. Toda
--      observación deberá registrar un motivo obligatorio y retornar el caso al
--      flujo de subsanación correspondiente."
--
-- El "flujo de subsanación correspondiente" es el que ya existe (§10.4-10.6): la
-- planilla vuelve al estado Observado (3) y el consolidador/colaborador elige
-- entre recargar el Consolidado del S10 él mismo o pedirle la corrección al
-- Coordinador ERP. Tesorería NO le escribe directamente al ERP: el ERP recibe
-- los pedidos del consolidador, que es quien tiene el «MOTIVO *» (RG-21).
--
-- Por eso NO hace falta un estado nuevo. Lo que sí hace falta es saber QUIÉN
-- observó, y eso es lo único que agrega este script:
--
-- 1) ga_origen_observacion_reembolso: catálogo de dos filas (Jefatura /
--    Tesorería). Va en tabla y no como texto en la salida porque es un dato
--    predefinido, como todos los demás estados del módulo.
--
-- 2) ga_solicitud_salida.observacion_reembolso_origen_id: de dónde vino la
--    observación que hoy tiene la salida. Sirve para tres cosas concretas:
--      • la bandeja de Tesorería sigue mostrando lo que ELLA observó (si no,
--        observar haría desaparecer la fila y nadie podría seguirle el rastro),
--        sin llenarse de lo que observó la jefatura, que no es asunto suyo;
--      • el colaborador ve "Observación de Tesorería" y no cree que fue su jefe;
--      • el correo que sale es el de Tesorería y no el de la jefatura.
--
-- 3) ga_correccion_s10.motivo_origen_id: la corrección copia la observación que
--    la originó (motivo_jefatura) para que el ERP la lea; ahora copia también de
--    dónde salió, o la bandeja del ERP la rotularía siempre como de la jefatura.
--
-- 4) ga_correo_evento REEMBOLSO_OBSERVADO_TESORERIA: el aviso al colaborador. Es
--    un evento aparte de REEMBOLSO_RECHAZADO (el de la jefatura) porque se
--    ORIGINA en otra pantalla —Reembolsos— y por lo tanto se administra desde
--    ahí, que es la regla del catálogo de correos.
--
-- ORDEN DE EJECUCIÓN
--   • Correr ANTES de desplegar el backend. EF materializa GaSolicitudSalida
--     entera en todo el módulo de salidas, así que desplegar primero tumba la
--     sección completa con 42703 (no solo la pantalla nueva). Al revés no hay
--     problema: el backend viejo no lee ni escribe ninguna columna nueva.
--   • Correr DESPUÉS de 2026-09-09_correcciones_s10_coordinador_erp.sql (crea
--     ga_correccion_s10) y de 20260908_GaCorreosPorPantallaYPlazoRendicion.sql
--     (crea ga_correo_pantalla). Las guardas de abajo abortan si falta alguno.
--
-- QUÉ PASA CON LO QUE YA ESTABA EN CURSO
--   Las observaciones que ya existen son todas de la jefatura (Tesorería no
--   podía observar), así que se marcan como tales en el paso 2.1. Ninguna fila
--   queda sin camino y ninguna cambia de estado.
--
-- Re-ejecutable: los IF NOT EXISTS / ON CONFLICT / WHERE NOT EXISTS dejan cada
-- sentencia sin efecto si ya se corrió. Correr TODO junto (no hay pasos "solo
-- después del deploy").
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252) y esos bytes se rechazan, así que se
-- fija acá: el script queda correcto sin depender de cómo se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

-- ── 0) Prerrequisitos ───────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_name = 'ga_correo_pantalla')
    THEN
        RAISE EXCEPTION
            'Falta ga_correo_pantalla. Correr primero Migrations/Manual/20260908_GaCorreosPorPantallaYPlazoRendicion.sql.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_name = 'ga_correccion_s10')
    THEN
        RAISE EXCEPTION
            'Falta ga_correccion_s10. Correr primero Migrations_Manual/2026-09-09_correcciones_s10_coordinador_erp.sql.';
    END IF;
END $$;

-- ── 1) Catálogo de orígenes de la observación ───────────────────────────────
-- Ids fijos por diseño: los usa EstadosSalida.OrigenObservacionReembolso. Los
-- nombres describen el PASO del flujo, que es lo que la pantalla muestra tal
-- cual ("Observación de Tesorería").

CREATE TABLE IF NOT EXISTS ga_origen_observacion_reembolso (
    id          integer PRIMARY KEY,
    descripcion varchar(60) NOT NULL,
    orden       integer NOT NULL DEFAULT 0,
    activo      boolean NOT NULL DEFAULT true
);

COMMENT ON TABLE ga_origen_observacion_reembolso IS
  'Quien devolvio el reembolso con una observacion: la jefatura en la segunda revision, o Tesoreria en la revision documental previa al pago (RG-49). El estado de la salida es el mismo (Observado); lo que cambia es quien espera la subsanacion y que correo salio.';

INSERT INTO ga_origen_observacion_reembolso (id, descripcion, orden) VALUES
    (1, 'Jefatura',  1),
    (2, 'Tesorería', 2)
ON CONFLICT (id) DO UPDATE SET descripcion = EXCLUDED.descripcion, orden = EXCLUDED.orden;

-- ── 2) De dónde vino la observación que tiene la salida ─────────────────────
-- NULLABLE a propósito: una salida sin observar no tiene origen, y las filas
-- viejas tampoco lo tenían. Ponerlo NOT NULL con un default rompería todo
-- INSERT que no lo mande y mentiría sobre las salidas nunca observadas.

ALTER TABLE ga_solicitud_salida
    ADD COLUMN IF NOT EXISTS observacion_reembolso_origen_id integer;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE  conname = 'fk_ga_solicitud_salida_observacion_reembolso_origen'
    ) THEN
        ALTER TABLE ga_solicitud_salida
            ADD CONSTRAINT fk_ga_solicitud_salida_observacion_reembolso_origen
            FOREIGN KEY (observacion_reembolso_origen_id)
            REFERENCES ga_origen_observacion_reembolso (id);
    END IF;
END $$;

COMMENT ON COLUMN ga_solicitud_salida.observacion_reembolso_origen_id IS
    'Quien escribio la observacion que esta en observacion_reembolso: la jefatura (1) o Tesoreria (2). Se limpia junto con la observacion cuando la jefatura aprueba.';

-- 2.1) Las observaciones que ya existen son todas de la jefatura: Tesoreria no
-- tenia como observar hasta este cambio. Se marcan para que la bandeja de
-- Tesoreria no las adopte y para que el colaborador siga viendo de quien son.
UPDATE ga_solicitud_salida
   SET observacion_reembolso_origen_id = 1
 WHERE observacion_reembolso_origen_id IS NULL
   AND observacion_reembolso IS NOT NULL
   AND btrim(observacion_reembolso) <> '';

-- La bandeja de Tesoreria pregunta por este par en cada listado.
CREATE INDEX IF NOT EXISTS ix_ga_solicitud_salida_reembolso_observado
    ON ga_solicitud_salida (estado_reembolso_id, observacion_reembolso_origen_id);

-- ── 3) La corrección al ERP recuerda de dónde salió la observación ──────────
-- ga_correccion_s10.motivo_jefatura es una COPIA de la observacion tomada al
-- solicitar (la de la salida se pisa si vuelven a observar). El nombre de esa
-- columna se conserva —renombrarla no aporta y toca codigo ya estable— pero
-- ahora puede traer la de Tesoreria, asi que se guarda cual de las dos es.

ALTER TABLE ga_correccion_s10
    ADD COLUMN IF NOT EXISTS motivo_origen_id integer;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE  conname = 'fk_ga_correccion_s10_motivo_origen'
    ) THEN
        ALTER TABLE ga_correccion_s10
            ADD CONSTRAINT fk_ga_correccion_s10_motivo_origen
            FOREIGN KEY (motivo_origen_id)
            REFERENCES ga_origen_observacion_reembolso (id);
    END IF;
END $$;

COMMENT ON COLUMN ga_correccion_s10.motivo_origen_id IS
    'Quien escribio la observacion copiada en motivo_jefatura: la jefatura (1) o Tesoreria (2). Sin esto la bandeja del ERP la rotularia siempre como de la jefatura.';

-- Las correcciones ya registradas nacieron de una observacion de jefatura.
UPDATE ga_correccion_s10
   SET motivo_origen_id = 1
 WHERE motivo_origen_id IS NULL
   AND motivo_jefatura IS NOT NULL
   AND btrim(motivo_jefatura) <> '';

-- ── 4) El correo de la observación de Tesorería ─────────────────────────────
-- Va en la pantalla REEMBOLSOS porque ahi se ORIGINA, que es como se reparte
-- todo el catalogo. Es el segundo correo propio de esa pantalla.
--
-- permite_desactivar_envio = false: si se apaga, el colaborador no se entera de
-- que su reembolso volvio y la planilla se queda esperando a alguien que no sabe
-- que tiene que actuar. El principal tampoco se puede desactivar por lo mismo.

INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, orden,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal, pantalla_id)
SELECT
    'REEMBOLSO_OBSERVADO_TESORERIA',
    'Reembolso observado por Tesorería · al solicitante',
    'Avisa al colaborador que Tesoreria devolvio su reembolso antes de pagarlo, con el motivo y los dos caminos para subsanar: recargar el Consolidado del S10 o pedirle la correccion al Coordinador ERP. Sale una vez por planilla y trabajador.',
    16,
    'El solicitante', true, false, false,
    p.id
FROM   ga_correo_pantalla p
WHERE  p.codigo = 'REEMBOLSOS' AND p.state
  AND  NOT EXISTS (SELECT 1 FROM ga_correo_evento e
                   WHERE lower(e.codigo) = 'reembolso_observado_tesoreria' AND e.state);

COMMIT;

-- ============================================================================
-- Verificación (correr después del COMMIT)
-- ============================================================================
-- SELECT * FROM ga_origen_observacion_reembolso ORDER BY id;   -- 2 filas
--
-- SELECT column_name, data_type, is_nullable
-- FROM   information_schema.columns
-- WHERE  (table_name = 'ga_solicitud_salida' AND column_name = 'observacion_reembolso_origen_id')
--    OR  (table_name = 'ga_correccion_s10'   AND column_name = 'motivo_origen_id');
--
-- Cuántas observaciones vivas hay y de quién (antes del deploy, todas 1):
-- SELECT observacion_reembolso_origen_id, count(*)
-- FROM   ga_solicitud_salida
-- WHERE  estado_reembolso_id = 3
-- GROUP  BY 1;
--
-- SELECT p.codigo AS pantalla, e.orden, e.codigo, e.nombre
-- FROM   ga_correo_evento e
-- JOIN   ga_correo_pantalla p ON p.id = e.pantalla_id
-- WHERE  e.state AND p.codigo = 'REEMBOLSOS'
-- ORDER  BY e.orden;
-- ============================================================================
