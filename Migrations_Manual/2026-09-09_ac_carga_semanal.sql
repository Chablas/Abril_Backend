-- Snapshot semanal de la carga (ponderada por tipo de partida) por supervisor/arquitecto,
-- para poder ver en el histórico si la sobrecarga es puntual o un patrón repetido.
-- Se llena desde el mismo cron que pega a POST /api/v1/ArquitecturaComercial/avance-semanal/snapshot,
-- que ahora también invoca SnapshotCargaSemanal (ver ArquitecturaComercialController.SnapshotAvanceSemanal).

CREATE TABLE IF NOT EXISTS ac_carga_semanal (
    id               SERIAL PRIMARY KEY,
    user_id          INTEGER NOT NULL,
    semana           DATE NOT NULL,
    hitos            INTEGER NOT NULL DEFAULT 0,
    entregables      INTEGER NOT NULL DEFAULT 0,
    consultas        INTEGER NOT NULL DEFAULT 0,
    total            INTEGER NOT NULL DEFAULT 0,
    total_ponderado  NUMERIC(8,2) NOT NULL DEFAULT 0,
    created_at       TIMESTAMP NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ac_carga_semanal_user_semana
    ON ac_carga_semanal (user_id, semana);
