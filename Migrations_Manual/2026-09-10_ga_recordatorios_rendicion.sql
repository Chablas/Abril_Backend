-- ============================================================================
-- Gestion de Salidas: recordatorios del plazo de rendicion (RG-33 / RG-34)
-- ============================================================================
-- Faltaban los dos avisos que el requerimiento pide alrededor del plazo para
-- rendir las movilidades del mes anterior:
--
--   * APERTURA -- el PRIMER DIA HABIL del mes: se abrio el plazo.
--   * CIERRE   -- el ULTIMO DIA APTO PARA RENDIR: hoy vence.
--
-- El "ultimo dia apto" no es una fecha fija: sale de ga_rendicion_config
-- (dias_habiles_plazo) contado sobre los dias habiles del mes, salteando
-- sabados, domingos y los feriados de Configuracion -> Feriados. Es el mismo
-- calculo de CalendarioNoLaborable.LimiteDeRendicion que ya usa la pantalla.
--
-- Los dos recordatorios se configuran como cualquier otro correo del flujo
-- (interruptor maestro, destinatario principal y destinatarios extra), asi que
-- son filas de ga_correo_evento -- no hay tablas nuevas de configuracion.
--
-- Lo unico que hace falta es poder mostrarlos en su PROPIA seccion de
-- Solicitud de Salidas -> Configuracion, aparte de "Correos". De ahi el
-- catalogo ga_correo_grupo: ga_correo_evento.pantalla_id dice DONDE se
-- administra el correo y ga_correo_evento.grupo_id dice EN QUE SECCION de esa
-- pantalla aparece. Son dos hechos distintos y por eso son dos columnas.
--
-- Idempotente: se puede correr mas de una vez.
-- ============================================================================

-- El archivo esta en UTF-8 y trae texto acentuado. En Windows psql arranca con el
-- client_encoding del locale (WIN1252) y esos bytes se rechazan, asi que se fija aca:
-- el script queda correcto sin depender de como se invoque.
SET client_encoding TO 'UTF8';

BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Catalogo de secciones de la configuracion (ga_correo_grupo)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ga_correo_grupo (
    id          serial       PRIMARY KEY,
    codigo      varchar(50)  NOT NULL,
    nombre      varchar(150) NOT NULL,
    orden       integer      NOT NULL DEFAULT 0,
    active      boolean      NOT NULL DEFAULT true,
    state       boolean      NOT NULL DEFAULT true,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    updated_at  timestamptz
);

COMMENT ON TABLE ga_correo_grupo IS
    'Seccion de la pantalla de Configuracion en la que se administra cada correo '
    '(ga_correo_evento.grupo_id): CORREOS son los del flujo, RECORDATORIOS los que '
    'dispara el cron del plazo de rendicion.';

-- Un solo grupo vivo por codigo (mismo criterio que ga_correo_evento).
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_correo_grupo_codigo
    ON ga_correo_grupo (lower(codigo))
    WHERE state;

INSERT INTO ga_correo_grupo (codigo, nombre, orden)
SELECT v.codigo, v.nombre, v.orden
FROM (VALUES
    ('CORREOS',       'Correos',       1),
    ('RECORDATORIOS', 'Recordatorios', 2)
) AS v(codigo, nombre, orden)
WHERE NOT EXISTS (
    SELECT 1 FROM ga_correo_grupo g WHERE lower(g.codigo) = lower(v.codigo) AND g.state
);

-- ---------------------------------------------------------------------------
-- 2) ga_correo_evento.grupo_id
-- ---------------------------------------------------------------------------
-- Se agrega NULLABLE, se rellena y recien despues se pone NOT NULL: si naciera
-- NOT NULL sin default, la sentencia fallaria contra las 15 filas que ya existen.
ALTER TABLE ga_correo_evento ADD COLUMN IF NOT EXISTS grupo_id integer;

UPDATE ga_correo_evento
SET    grupo_id = (SELECT id FROM ga_correo_grupo WHERE codigo = 'CORREOS' AND state)
WHERE  grupo_id IS NULL;

ALTER TABLE ga_correo_evento ALTER COLUMN grupo_id SET NOT NULL;

-- DEFAULT = CORREOS. La columna es NOT NULL y hay scripts anteriores que insertan correos sin
-- conocerla (los seeds de la firma de Tesoreria y del Coordinador ERP); con el default, volver a
-- correr cualquiera de ellos sigue funcionando y el correo cae donde corresponde: en el flujo.
-- Se resuelve el id en tiempo de ejecucion porque un DEFAULT no admite subconsulta.
DO $$
DECLARE v_correos integer;
BEGIN
    SELECT id INTO v_correos FROM ga_correo_grupo WHERE codigo = 'CORREOS' AND state;
    EXECUTE format('ALTER TABLE ga_correo_evento ALTER COLUMN grupo_id SET DEFAULT %s', v_correos);
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ga_correo_evento_grupo_id_fkey'
    ) THEN
        ALTER TABLE ga_correo_evento
            ADD CONSTRAINT ga_correo_evento_grupo_id_fkey
            FOREIGN KEY (grupo_id) REFERENCES ga_correo_grupo(id);
    END IF;
END $$;

COMMENT ON COLUMN ga_correo_evento.grupo_id IS
    'Seccion de la pantalla de Configuracion donde aparece este correo (ga_correo_grupo). '
    'Ortogonal a pantalla_id, que dice en QUE pantalla se administra.';

-- ---------------------------------------------------------------------------
-- 3) Los dos recordatorios
-- ---------------------------------------------------------------------------
-- Cuelgan de SOLICITUD_SALIDAS: es la pantalla donde nacen las salidas que estos
-- avisos recuerdan rendir, y es donde el usuario los va a buscar.
--
-- Los dos arrancan con los dos interruptores en true y con
-- permite_desactivar_envio / permite_desactivar_principal en true: apagar el
-- recordatorio completo y sacar al trabajador del envio son justamente los dos
-- controles que la seccion tiene que ofrecer.
INSERT INTO ga_correo_evento (
    codigo, pantalla_id, grupo_id, nombre, descripcion, orden, active,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal, state
)
SELECT v.codigo,
       (SELECT id FROM ga_correo_pantalla WHERE codigo = 'SOLICITUD_SALIDAS' AND state),
       (SELECT id FROM ga_correo_grupo    WHERE codigo = 'RECORDATORIOS'     AND state),
       v.nombre, v.descripcion, v.orden, true,
       'El trabajador con salidas sin rendir', true,
       true, true, true
FROM (VALUES
    ('RECORDATORIO_RENDICION_APERTURA',
     'Primer día hábil · se abrió el plazo para rendir',
     'Se envía el primer día hábil del mes a cada trabajador con salidas del mes anterior aptas para rendir y todavía sin rendir.',
     1),
    ('RECORDATORIO_RENDICION_CIERRE',
     'Último día para rendir · vence el plazo',
     'Se envía el último día hábil del plazo (Días reembolsables) a cada trabajador que aún tiene salidas del mes anterior sin rendir.',
     2)
) AS v(codigo, nombre, descripcion, orden)
WHERE NOT EXISTS (
    SELECT 1 FROM ga_correo_evento e WHERE lower(e.codigo) = lower(v.codigo) AND e.state
);

COMMIT;

-- ---------------------------------------------------------------------------
-- Verificacion
-- ---------------------------------------------------------------------------
-- SELECT g.codigo AS grupo, p.codigo AS pantalla, e.codigo, e.orden, e.active
-- FROM   ga_correo_evento e
-- JOIN   ga_correo_grupo    g ON g.id = e.grupo_id
-- JOIN   ga_correo_pantalla p ON p.id = e.pantalla_id
-- WHERE  e.state
-- ORDER BY g.orden, p.orden, e.orden;
