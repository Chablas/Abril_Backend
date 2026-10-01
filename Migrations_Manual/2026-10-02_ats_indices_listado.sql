-- Índices para que el listado de ATS siga rápido con miles de filas (paginado + filtros por fecha/proyecto).
-- Solo crean índices: no tocan datos. IF NOT EXISTS por si alguno ya existe.

-- Orden por defecto del listado (fecha desc, id desc) y filtro por rango de fechas.
CREATE INDEX IF NOT EXISTS ix_ss_ats_fecha_id_desc ON ss_ats (fecha DESC, id DESC);

-- Filtro por proyecto + orden por fecha (el caso normal: cada usuario ve su proyecto).
CREATE INDEX IF NOT EXISTS ix_ss_ats_proyecto_fecha_id ON ss_ats (proyecto_id, fecha DESC, id DESC);

-- Conteo de adhesiones por grupo en el listado de ATS grupales.
CREATE INDEX IF NOT EXISTS ix_ss_ats_ats_grupo_id ON ss_ats (ats_grupo_id) WHERE ats_grupo_id IS NOT NULL;

-- Listado de grupos por proyecto/fecha.
CREATE INDEX IF NOT EXISTS ix_ss_ats_grupo_proyecto_fecha ON ss_ats_grupo (proyecto_id, fecha DESC, id DESC);

-- PETAR de cada ATS (se consulta por lote en cada página del listado).
CREATE INDEX IF NOT EXISTS ix_ss_petar_ats_id ON ss_petar (ats_id);
