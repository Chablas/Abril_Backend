-- Kits/BOM (Botiquín, Estación de Emergencia) ahora persiste en el presupuesto real, en vez de ser
-- solo una calculadora de pantalla que se perdía al recargar. Tabla propia (no reutiliza
-- ss_presupuesto_item_metrado, que ya usa Servicios de costo fijo — mezclarlos ahí rompería el
-- DELETE-and-reinsert de cada guardado, borrando los datos del otro).
CREATE TABLE ss_presupuesto_kit_item (
    id                serial PRIMARY KEY,
    presupuesto_id    int NOT NULL REFERENCES ss_presupuesto(id),
    kit_id            int NOT NULL REFERENCES ss_kit(id),
    cantidad_kits     numeric(10,2) NOT NULL,
    familia_id        int NOT NULL REFERENCES ss_material_familia(id),
    cantidad_por_kit  numeric(12,4) NOT NULL,
    cantidad_total    numeric(14,4) NOT NULL,
    precio_unitario   numeric(12,4) NOT NULL DEFAULT 0,
    total             numeric(14,2) NOT NULL DEFAULT 0,
    es_consumible     boolean NOT NULL DEFAULT true
);

CREATE INDEX ix_presupuesto_kit_item_presupuesto ON ss_presupuesto_kit_item (presupuesto_id);
