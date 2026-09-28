-- Kit de marca (colores secundario/terciario/texto + estilos de texto por rol) y flag de
-- plantilla reutilizable para el módulo de Cursos (Abril-Frontend, features/cursos).
-- Ver Features/CursoModule/Infrastructure/Models/Curso.cs (propiedades agregadas 26-sep-2026).

ALTER TABLE curso ADD COLUMN IF NOT EXISTS color_marca_secundario   VARCHAR;
ALTER TABLE curso ADD COLUMN IF NOT EXISTS color_marca_terciario    VARCHAR;
ALTER TABLE curso ADD COLUMN IF NOT EXISTS color_texto_marca        VARCHAR;
ALTER TABLE curso ADD COLUMN IF NOT EXISTS estilos_texto_marca_json TEXT;
ALTER TABLE curso ADD COLUMN IF NOT EXISTS es_plantilla             BOOLEAN NOT NULL DEFAULT false;
