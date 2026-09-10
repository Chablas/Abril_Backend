-- Migración manual (pgAdmin) — módulo de Costos de Arquitectura Comercial.
-- Ejecutar directamente contra la BD PostgreSQL. No usar dotnet ef.
--
-- Presupuesto aprobado por partida para todo el proyecto (no por mes) — el techo contra el
-- que se mide el gasto real acumulado (ac_costo_registros, todos los meses) para sacar la
-- desviación %. Complementa ac_costo_registros / ac_costo_proyecciones / ac_costo_meta_mensuales
-- creadas en 20260902_CreateAcCostos.sql.

CREATE TABLE IF NOT EXISTS ac_costo_presupuestos (
    id           SERIAL PRIMARY KEY,
    proyecto_id  INTEGER NOT NULL REFERENCES project (project_id) ON DELETE CASCADE,
    partida      TEXT NOT NULL,   -- Mano de Obra | Materiales | Subcontrata (lista fija en código)
    monto        NUMERIC(14, 2) NOT NULL DEFAULT 0,
    creado_por   TEXT,
    created_at   TIMESTAMP NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at   TIMESTAMP,
    CONSTRAINT uq_ac_costo_presupuestos UNIQUE (proyecto_id, partida)
);

CREATE INDEX IF NOT EXISTS ix_ac_costo_presupuestos_proyecto_id ON ac_costo_presupuestos (proyecto_id);
