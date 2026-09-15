-- ============================================================================
-- Plan de acción del Jefe SSOMA sobre sus resultados de evaluación
-- (ver ev_evaluacion_jefe_ssoma / ev_evaluacion_jefe_ssoma_detalle).
--
-- El Jefe SSOMA (único puesto, ver PuestoIds.JefeSsoma) redacta, por período,
-- una acción concreta con meta medible y fecha límite para cada criterio en el
-- que salió bajo — así la pantalla de Resultados no solo muestra el promedio,
-- sino el compromiso de mejora y su seguimiento período a período.
--
-- No lleva evaluador ni nada del lado anónimo de la evaluación: es contenido
-- del EVALUADO (Jefe SSOMA), visible también para quien vea Resultados.
--
-- Idempotente: usa IF NOT EXISTS, se puede re-correr.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS ev_jefe_ssoma_plan_accion (
    id                  serial PRIMARY KEY,
    periodo_id          int NOT NULL REFERENCES ev_periodo(id),
    plantilla_id        int REFERENCES ev_jefe_ssoma_plantilla(id),
    criterio            varchar(300) NOT NULL,
    accion              text NOT NULL,
    meta                text NOT NULL,
    fecha_limite        date,
    estado              varchar(20) NOT NULL DEFAULT 'Pendiente',
    created_by_user_id  int NOT NULL REFERENCES app_user(user_id),
    created_at          timestamp NOT NULL DEFAULT now(),
    updated_at          timestamp
);

CREATE INDEX IF NOT EXISTS ix_ev_jefe_ssoma_plan_accion_periodo
    ON ev_jefe_ssoma_plan_accion (periodo_id);

COMMIT;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT table_name FROM information_schema.tables WHERE table_name = 'ev_jefe_ssoma_plan_accion';
-- Esperado: 1 fila.
