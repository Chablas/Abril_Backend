-- Agrega columnas faltantes en project usadas por EmoAlertaController.MiResumen
-- para resolver el proyecto actual de un coordinador (admin/ssoma) por email.

ALTER TABLE project ADD COLUMN IF NOT EXISTS email_coord_admin varchar;
ALTER TABLE project ADD COLUMN IF NOT EXISTS email_coord_ssoma varchar;
