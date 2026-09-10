-- Migración manual (pgAdmin) — módulo de Costos de Arquitectura Comercial.
-- Ejecutar directamente contra la BD PostgreSQL. No usar dotnet ef.
--
-- Cierre de periodo: marca un (proyecto, año, mes) como cerrado. Mientras esté cerrado,
-- el registro semanal de ese mes no se puede editar (UpsertRegistro lo valida en backend).
-- Reabrir el periodo simplemente borra la fila.

CREATE TABLE IF NOT EXISTS ac_costo_cierres (
    id           SERIAL PRIMARY KEY,
    proyecto_id  INTEGER NOT NULL REFERENCES project (project_id) ON DELETE CASCADE,
    anio         INTEGER NOT NULL,
    mes          INTEGER NOT NULL,
    cerrado_por  TEXT,
    cerrado_en   TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT uq_ac_costo_cierres UNIQUE (proyecto_id, anio, mes)
);

CREATE INDEX IF NOT EXISTS ix_ac_costo_cierres_proyecto_id ON ac_costo_cierres (proyecto_id);
