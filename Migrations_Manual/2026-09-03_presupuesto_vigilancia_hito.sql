-- Servicio de vigilancia externa por hito del cronograma (facturado por punto/turno, no por
-- vigilante — eso es el rol interno VIGIA de ss_presupuesto_personal_hito, tabla aparte).
-- Precio unitario: snapshot del ratio calculado en Ratios -> Catálogo para la família
-- "Servicio de Vigilancia" al momento de guardar (mismo mecanismo que el resto de materiales).

CREATE TABLE IF NOT EXISTS ss_presupuesto_vigilancia_hito (
    id               SERIAL PRIMARY KEY,
    presupuesto_id   INTEGER NOT NULL REFERENCES ss_presupuesto(id) ON DELETE CASCADE,
    hito_id          INTEGER NOT NULL REFERENCES milestone_schedule(milestone_schedule_id),
    hito_salida_id   INTEGER NULL REFERENCES milestone_schedule(milestone_schedule_id),
    cantidad_puntos  INTEGER NOT NULL DEFAULT 0,
    semanas          NUMERIC(10,2) NOT NULL DEFAULT 0,
    precio_unitario  NUMERIC(12,2) NOT NULL DEFAULT 0,
    total            NUMERIC(14,2) NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_ss_presupuesto_vigilancia_hito_presupuesto_id
    ON ss_presupuesto_vigilancia_hito(presupuesto_id);
