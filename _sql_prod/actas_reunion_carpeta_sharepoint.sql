-- ─────────────────────────────────────────────────────────────────────────────
-- Actas de Reunión → Carpeta de SharePoint/OneDrive para los archivos adjuntos.
-- Tabla singleton (a lo sumo una fila vigente con state = true): guarda el link
-- pegado por el usuario y su ubicación estable resuelta vía Graph (driveId+folderId).
-- Si no hay fila vigente, los adjuntos siguen yendo al storage por defecto (Azure).
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS reunion_folder (
    reunion_folder_id  SERIAL PRIMARY KEY,
    link_url           VARCHAR(2000) NOT NULL,
    drive_id           VARCHAR(500)  NOT NULL,
    folder_id          VARCHAR(500)  NOT NULL,
    folder_name        VARCHAR(500),
    web_url            VARCHAR(2000),
    created_date_time  TIMESTAMPTZ   NOT NULL,
    created_user_id    INT           NOT NULL,
    updated_date_time  TIMESTAMPTZ,
    updated_user_id    INT,
    active             BOOLEAN       NOT NULL DEFAULT TRUE,
    state              BOOLEAN       NOT NULL DEFAULT TRUE
);

-- Singleton: a lo sumo una fila vigente (state = true).
CREATE UNIQUE INDEX IF NOT EXISTS ux_reunion_folder_singleton
    ON reunion_folder ((TRUE))
    WHERE state = TRUE;
