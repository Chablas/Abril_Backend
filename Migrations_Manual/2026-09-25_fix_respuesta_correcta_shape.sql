-- BUG REAL: la corrección genérica (CursoIntentoService.CorregirGenerico) compara el
-- RespuestaJson COMPLETO que emite el reproductor contra "respuestaCorrecta" con igualdad
-- exacta de JSON. El reproductor SIEMPRE envuelve la respuesta en un objeto con una clave
-- semántica ({valor}, {opcionId}, {ordenIds}) — ver slide-verdadero-falso.ts,
-- slide-opcion-multiple.ts, slide-ordenar.ts — pero el seed de 2026-09-24 guardó
-- "respuestaCorrecta" como el valor "pelado" (bool/string/array), sin ese envoltorio.
-- Resultado: DeepEquals(pelado, envuelto) siempre es false — las 3 preguntas evaluables
-- del curso "Riesgo al subir y bajar escaleras fijas" califican mal SIEMPRE, sin importar
-- la respuesta real del alumno.

-- Verificación ANTES de corregir (solo lectura) — confirma el problema:
-- SELECT id, orden, tipo_codigo, configuracion_json->'respuestaCorrecta' AS respuesta_correcta_actual
-- FROM curso_slide
-- WHERE curso_id = (SELECT id FROM curso WHERE titulo = 'Riesgo al subir y bajar escaleras fijas')
--   AND tipo_codigo IN ('pregunta_vf', 'pregunta_opcion_multiple', 'pregunta_ordenar')
-- ORDER BY orden;

-- Corrección: envuelve cada respuestaCorrecta en la clave que el reproductor realmente envía.

-- Slide 8 (pregunta_ordenar): respuestaCorrecta ["1","2","3","4"] -> {"ordenIds": [...]}
UPDATE curso_slide
SET configuracion_json = jsonb_set(
  configuracion_json,
  '{respuestaCorrecta}',
  jsonb_build_object('ordenIds', configuracion_json->'respuestaCorrecta')
)
WHERE curso_id = (SELECT id FROM curso WHERE titulo = 'Riesgo al subir y bajar escaleras fijas')
  AND tipo_codigo = 'pregunta_ordenar'
  AND jsonb_typeof(configuracion_json->'respuestaCorrecta') = 'array';

-- Slides 9 y 10 (pregunta_opcion_multiple): respuestaCorrecta "c" -> {"opcionId": "c"}
UPDATE curso_slide
SET configuracion_json = jsonb_set(
  configuracion_json,
  '{respuestaCorrecta}',
  jsonb_build_object('opcionId', configuracion_json->'respuestaCorrecta')
)
WHERE curso_id = (SELECT id FROM curso WHERE titulo = 'Riesgo al subir y bajar escaleras fijas')
  AND tipo_codigo = 'pregunta_opcion_multiple'
  AND jsonb_typeof(configuracion_json->'respuestaCorrecta') = 'string';

-- Slide 11 (pregunta_vf): respuestaCorrecta false -> {"valor": false}
UPDATE curso_slide
SET configuracion_json = jsonb_set(
  configuracion_json,
  '{respuestaCorrecta}',
  jsonb_build_object('valor', configuracion_json->'respuestaCorrecta')
)
WHERE curso_id = (SELECT id FROM curso WHERE titulo = 'Riesgo al subir y bajar escaleras fijas')
  AND tipo_codigo = 'pregunta_vf'
  AND jsonb_typeof(configuracion_json->'respuestaCorrecta') = 'boolean';

-- Verificación DESPUÉS (debe mostrar los mismos 4 registros, ahora envueltos):
-- SELECT id, orden, tipo_codigo, configuracion_json->'respuestaCorrecta' AS respuesta_correcta_nueva
-- FROM curso_slide
-- WHERE curso_id = (SELECT id FROM curso WHERE titulo = 'Riesgo al subir y bajar escaleras fijas')
--   AND tipo_codigo IN ('pregunta_vf', 'pregunta_opcion_multiple', 'pregunta_ordenar')
-- ORDER BY orden;
