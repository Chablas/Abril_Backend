-- Agrega el logotipo de marca del curso (para insertarlo rápido como elemento en el lienzo libre).
-- Mismo patrón que color_tema (2026-09-24_curso_color_tema.sql).

ALTER TABLE curso ADD COLUMN IF NOT EXISTS logo_url text NULL;

-- Verificación (solo lectura):
-- SELECT id, titulo, color_tema, logo_url FROM curso ORDER BY id;
