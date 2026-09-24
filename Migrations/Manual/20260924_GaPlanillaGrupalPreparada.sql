-- ============================================================================
-- Gestión Administrativa · Salidas — La planilla grupal se prepara ANTES del S10
-- Fecha: 2026-09-24
--
-- Hasta hoy la planilla grupal («PLANILLA DE REEMBOLSO») la armaba Abril One en
-- el mismo acto en que el consolidador subía el Consolidado del S10, y se
-- guardaba en ga_consolidado_s10.planilla_grupal_*. Desde hoy el consolidador la
-- PREPARA primero en Gestión de Rendiciones —es el papel con el que registra las
-- planillas en el S10— y recién después sube el consolidado, que se sube sobre
-- esa planilla y ya no la genera. La jefatura la sigue firmando en Consolidados.
--
-- Como la planilla existe antes que el consolidado, vive en su propia tabla:
--
--   ga_planilla_grupal           el documento, con el código CONS-ÁREA-AAAA-NNN
--                                (el código de la rendición grupal nace acá)
--   ga_planilla_grupal_rendicion qué planillas de rendición cubre
--   ga_consolidado_s10.planilla_grupal_id
--                                de qué planilla preparada salió el consolidado
--                                (null en los anteriores a este cambio)
--
-- Al subir el S10 el consolidado toma el código de la planilla y copia su
-- archivo en ga_consolidado_s10.planilla_grupal_*, que es de donde lo siguen
-- leyendo Consolidados, Correcciones S10 y Reembolsos.
--
-- Correr ANTES de desplegar el backend: EF lee ga_consolidado_s10 entera en todo
-- el módulo, y sin planilla_grupal_id cae con 42703 en Gestión de Rendiciones,
-- Consolidados, Correcciones S10 y Reembolsos. El backend viejo sigue
-- funcionando igual con este script aplicado.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev, demo y prod.
-- ============================================================================

-- El archivo está en UTF-8 y trae texto acentuado. En Windows psql arranca con
-- el client_encoding del locale (WIN1252): se fija acá para no depender de eso.
SET client_encoding TO 'UTF8';

BEGIN;

CREATE TABLE IF NOT EXISTS ga_planilla_grupal (
    id                serial      PRIMARY KEY,
    codigo            text        NOT NULL,
    anio              integer     NOT NULL,
    numero            integer     NOT NULL,
    area_scope_id     integer     NULL,
    pdf_url           text        NOT NULL,
    pdf_item_id       text        NULL,
    pdf_drive_id      text        NULL,
    pdf_filename      text        NOT NULL,
    preparada_por_id  integer     NOT NULL,
    preparada_at      timestamptz NOT NULL DEFAULT now(),
    state             boolean     NOT NULL DEFAULT true,
    CONSTRAINT fk_ga_planilla_grupal_area_scope
        FOREIGN KEY (area_scope_id) REFERENCES area_scope(area_scope_id),
    CONSTRAINT fk_ga_planilla_grupal_preparada_por
        FOREIGN KEY (preparada_por_id) REFERENCES app_user(user_id),
    CONSTRAINT chk_ga_planilla_grupal_codigo CHECK (btrim(codigo) <> '')
);

-- Un código vive en una sola planilla vigente. Una planilla grupal ya preparada
-- no se rehace ni se reemplaza: es lo que el consolidador registró en el S10.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_planilla_grupal_codigo
    ON ga_planilla_grupal (codigo) WHERE state;

CREATE INDEX IF NOT EXISTS ix_ga_planilla_grupal_preparada_por_id
    ON ga_planilla_grupal (preparada_por_id);

COMMENT ON TABLE ga_planilla_grupal IS
    'Planilla grupal (PLANILLA DE REEMBOLSO) que prepara el consolidador antes de subir el '
    'Consolidado del S10. El código CONS-ÁREA-AAAA-NNN nace acá y el consolidado lo hereda. '
    'Una vez preparada no se rehace. state=false = dada de baja.';

CREATE TABLE IF NOT EXISTS ga_planilla_grupal_rendicion (
    id                 serial  PRIMARY KEY,
    planilla_grupal_id integer NOT NULL,
    rendicion_id       integer NOT NULL,
    state              boolean NOT NULL DEFAULT true,
    CONSTRAINT fk_ga_planilla_grupal_rendicion_planilla
        FOREIGN KEY (planilla_grupal_id) REFERENCES ga_planilla_grupal(id),
    CONSTRAINT fk_ga_planilla_grupal_rendicion_rendicion
        FOREIGN KEY (rendicion_id) REFERENCES ga_rendicion(id)
);

-- Una planilla de rendición está en una sola planilla grupal vigente a la vez.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_planilla_grupal_rendicion_una_vigente
    ON ga_planilla_grupal_rendicion (rendicion_id) WHERE state;

-- Y cada planilla grupal la vincula una sola vez. Sirve además de índice para ir
-- de la planilla grupal a sus rendiciones.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_planilla_grupal_rendicion_par
    ON ga_planilla_grupal_rendicion (planilla_grupal_id, rendicion_id);

COMMENT ON TABLE ga_planilla_grupal_rendicion IS
    'Qué planillas de rendición cubre cada planilla grupal. Como máximo un vínculo vigente por '
    'rendición: una rendición no entra en dos planillas grupales. state=false = vínculo dado de baja.';

-- ── ga_consolidado_s10 ──────────────────────────────────────────────────────
ALTER TABLE ga_consolidado_s10
    ADD COLUMN IF NOT EXISTS planilla_grupal_id integer NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_consolidado_s10_planilla_grupal'
    ) THEN
        ALTER TABLE ga_consolidado_s10
            ADD CONSTRAINT fk_ga_consolidado_s10_planilla_grupal
            FOREIGN KEY (planilla_grupal_id) REFERENCES ga_planilla_grupal(id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_ga_consolidado_s10_planilla_grupal_id
    ON ga_consolidado_s10 (planilla_grupal_id);

COMMENT ON COLUMN ga_consolidado_s10.planilla_grupal_id IS
    'Planilla grupal preparada sobre la que se subió el consolidado (su archivo se copia en '
    'planilla_grupal_*). Null en los consolidados anteriores al 2026-09-24.';

COMMIT;
