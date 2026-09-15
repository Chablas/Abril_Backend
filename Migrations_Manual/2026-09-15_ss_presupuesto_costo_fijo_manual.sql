-- Montos fijos tipeados a mano por proyecto (Malla Anticaída, Encapsulado, Malla Anillo Fenólico)
-- — a diferencia de Materiales/Servicios fijos, estos NO salen de ningún ratio histórico: el costo
-- real depende de la geometría/altura del edificio de cada obra puntual, así que el responsable
-- SSOMA los tipea directo como un monto "glb" (global), igual que en el presupuesto de referencia
-- de Costos.

CREATE TABLE IF NOT EXISTS ss_presupuesto_costo_fijo_manual (
    id                     SERIAL PRIMARY KEY,
    project_id             INTEGER NOT NULL UNIQUE REFERENCES project(project_id),
    malla_anticaida        NUMERIC(12,2) NOT NULL DEFAULT 0,
    encapsulado            NUMERIC(12,2) NOT NULL DEFAULT 0,
    malla_anillo_fenolico  NUMERIC(12,2) NOT NULL DEFAULT 0,
    notas                  TEXT NULL,
    actualizado_en         TIMESTAMPTZ NOT NULL DEFAULT now()
);
