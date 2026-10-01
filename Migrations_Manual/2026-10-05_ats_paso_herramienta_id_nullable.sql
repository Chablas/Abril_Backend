-- Los pasos de las actividades de una plantilla y los pasos/herramientas escritos a mano ("de una sola vez") NO tienen
-- id de catálogo: el modelo ya lo permite (PasoId? / HerramientaId?) pero las columnas se crearon NOT NULL, y por eso
-- adherirse a un ATS grupal con esos pasos fallaba con "null value in column paso_id ... violates not-null constraint".
ALTER TABLE ss_ats_paso_seleccionado        ALTER COLUMN paso_id        DROP NOT NULL;
ALTER TABLE ss_ats_herramienta_seleccionada ALTER COLUMN herramienta_id DROP NOT NULL;
