CREATE TABLE IF NOT EXISTS ss_indicador_empresa_excluida (
    empresa_id    INT PRIMARY KEY,
    motivo        TEXT,
    excluido_por  INT,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);
