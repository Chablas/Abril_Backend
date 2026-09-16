-- ============================================================================
-- Centro de aprendizaje — se elimina el color de acento de los grupos.
-- Fecha: 2026-09-16
--
-- El campo «Color de acento (opcional)» salió del modal de grupos y las pantallas usan
-- siempre el teal de Abril, así que la columna deja de leerse y escribirse en toda la
-- aplicación.
--
-- ORDEN: correr DESPUÉS de desplegar el backend. El backend desplegado hoy todavía mapea
-- la columna en EF y, si se borra antes, el modal del login, el Centro de aprendizaje del
-- inicio y su configuración responden 500 (42703) hasta el deploy.
--
-- Idempotente.
-- ============================================================================

ALTER TABLE learning_category DROP COLUMN IF EXISTS accent_color;
