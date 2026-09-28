-- "Solo práctica": permite que una pregunta evaluable siga mostrando acierto/error en el
-- reproductor (EsEvaluable=true, corre la corrección genérica) pero NO sume puntaje a la
-- nota final del intento (ver CursoIntentoService.FinalizarAsync).
-- Default true: todo lo ya sembrado sigue contando para la nota exactamente igual que hoy.

ALTER TABLE curso_slide ADD COLUMN IF NOT EXISTS contar_para_nota boolean NOT NULL DEFAULT true;

-- Verificación (solo lectura):
-- SELECT id, orden, tipo_codigo, es_evaluable, contar_para_nota FROM curso_slide ORDER BY curso_id, orden;
