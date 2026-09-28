-- Banco de preguntas reutilizable entre cursos (ver CursoPreguntaBanco.cs).
-- Guarda una COPIA del configuracion_json de una pregunta evaluable (V/F, opción múltiple,
-- ordenar, etc.) para poder clonarla al armar otros cursos, sin quedar enlazada al original
-- (editar una entrada del banco después nunca debe alterar exámenes ya rendidos en otros cursos).

CREATE TABLE IF NOT EXISTS curso_pregunta_banco (
  id SERIAL PRIMARY KEY,
  tipo_codigo TEXT NOT NULL,
  titulo TEXT NOT NULL,
  categoria TEXT NULL,
  puntaje_sugerido NUMERIC NULL,
  configuracion_json JSONB NOT NULL DEFAULT '{}'::jsonb,
  activo BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Verificación (solo lectura):
-- SELECT id, tipo_codigo, titulo, categoria, activo, created_at FROM curso_pregunta_banco ORDER BY id;
