CREATE TABLE IF NOT EXISTS ss_desempeno_supervisor_excluido (
    worker_id     INT PRIMARY KEY REFERENCES workers(id),
    motivo        TEXT,
    excluido_por  INT,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);
