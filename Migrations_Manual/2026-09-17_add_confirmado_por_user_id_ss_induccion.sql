-- Agrega auditoría de quién confirmó el ingreso a una inducción (ss_induccion).
-- Antes no había forma de saber qué usuario marcó IngresoConfirmado = true.
ALTER TABLE ss_induccion
    ADD COLUMN IF NOT EXISTS confirmado_por_user_id integer NULL;

ALTER TABLE ss_induccion
    ADD CONSTRAINT fk_ss_induccion_confirmado_por_user
    FOREIGN KEY (confirmado_por_user_id) REFERENCES app_user(user_id);
