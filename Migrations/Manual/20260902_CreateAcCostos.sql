-- Migración manual (pgAdmin) — módulo de Costos de Arquitectura Comercial.
-- Ejecutar directamente contra la BD PostgreSQL. No usar dotnet ef.
--
-- Conteo semanal de gasto por partida (Mano de Obra, Materiales, Subcontrata) del mes en
-- curso, proyección de gasto al mes siguiente por partida, y meta de presupuesto mensual
-- de la compañía (usada solo para el gráfico de evolución).

CREATE TABLE IF NOT EXISTS ac_costo_registros (
    id           SERIAL PRIMARY KEY,
    proyecto_id  INTEGER NOT NULL REFERENCES project (project_id) ON DELETE CASCADE,
    anio         INTEGER NOT NULL,
    mes          INTEGER NOT NULL,
    semana       INTEGER NOT NULL,
    partida      TEXT NOT NULL,   -- Mano de Obra | Materiales | Subcontrata (lista fija en código)
    monto        NUMERIC(14, 2) NOT NULL DEFAULT 0,
    creado_por   TEXT,
    created_at   TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at   TIMESTAMP,
    CONSTRAINT uq_ac_costo_registros UNIQUE (proyecto_id, anio, mes, semana, partida)
);

CREATE INDEX IF NOT EXISTS ix_ac_costo_registros_proyecto_id ON ac_costo_registros (proyecto_id);
CREATE INDEX IF NOT EXISTS ix_ac_costo_registros_anio_mes    ON ac_costo_registros (anio, mes);

CREATE TABLE IF NOT EXISTS ac_costo_proyecciones (
    id           SERIAL PRIMARY KEY,
    proyecto_id  INTEGER NOT NULL REFERENCES project (project_id) ON DELETE CASCADE,
    anio         INTEGER NOT NULL,
    mes          INTEGER NOT NULL,
    partida      TEXT NOT NULL,
    monto        NUMERIC(14, 2) NOT NULL DEFAULT 0,
    creado_por   TEXT,
    created_at   TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at   TIMESTAMP,
    CONSTRAINT uq_ac_costo_proyecciones UNIQUE (proyecto_id, anio, mes, partida)
);

CREATE INDEX IF NOT EXISTS ix_ac_costo_proyecciones_proyecto_id ON ac_costo_proyecciones (proyecto_id);
CREATE INDEX IF NOT EXISTS ix_ac_costo_proyecciones_anio_mes    ON ac_costo_proyecciones (anio, mes);

CREATE TABLE IF NOT EXISTS ac_costo_meta_mensuales (
    id           SERIAL PRIMARY KEY,
    anio         INTEGER NOT NULL,
    mes          INTEGER NOT NULL,
    monto        NUMERIC(14, 2) NOT NULL DEFAULT 0,
    creado_por   TEXT,
    created_at   TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at   TIMESTAMP,
    CONSTRAINT uq_ac_costo_meta_mensuales UNIQUE (anio, mes)
);
