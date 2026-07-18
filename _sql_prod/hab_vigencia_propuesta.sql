-- Renovación de documentos de habilitación (estado "Renovando").
-- Agrega la columna donde se guarda la fecha de vigencia propuesta por el contratista
-- durante una renovación, sin pisar la vigencia aprobada anterior.
--
-- IMPORTANTE: ejecutar en producción ANTES de desplegar el backend nuevo.
-- El modelo EF ya mapea esta columna; si el código sube antes que la columna exista,
-- las consultas sobre ss_hab_trabajador fallarán ("column vigencia_propuesta does not exist").

ALTER TABLE ss_hab_trabajador ADD COLUMN IF NOT EXISTS vigencia_propuesta date;
