-- Agrega el color de tema del curso (hex). Todas sus slides lo heredan por defecto en el
-- frontend vía SlideEstilo; una slide puntual puede sobreescribirlo si hace falta.
ALTER TABLE curso ADD COLUMN IF NOT EXISTS color_tema varchar(20) NULL;
