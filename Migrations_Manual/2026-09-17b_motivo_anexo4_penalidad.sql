-- Guarda la viñeta literal del Anexo 4 elegida dentro de la categoría de infracción (ej. "Robo
-- o intento de robo" dentro de "Grave"), seleccionada en un segundo combo del formulario de
-- Nueva Penalidad -- la descripción libre sigue existiendo aparte, para detalle adicional del
-- caso puntual.
ALTER TABLE ssoma_penalidad ADD COLUMN IF NOT EXISTS motivo varchar(300);
