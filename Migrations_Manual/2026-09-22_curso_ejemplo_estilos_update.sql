-- ============================================================================
-- Curso "Prevención de caídas" — agrega el bloque opcional "estilo" (fondo
-- animado del curso-player, ver SlideEstilo en el frontend) a algunas slides
-- de ejemplo.
--
-- Este script es un UPDATE, no un INSERT: el script original
-- 2026-09-22_curso_ejemplo_prevencion_caidas.sql probablemente ya fue corrido
-- en la base del usuario, así que en vez de tocarlo se agrega este script
-- nuevo que actualiza las slides ya existentes.
--
-- Usa concatenación jsonb (||) para NO perder el resto de configuracion_json
-- de cada slide (en particular, no toca ni borra la clave "respuestaCorrecta").
--
-- Deliberadamente solo se personalizan 3 de las 6 slides (1, 2 y 4). Las
-- demás (3, 5, 6) se dejan sin "estilo" para probar también la paleta de
-- fondo por defecto (rotación determinística por índice de slide) que aplica
-- el frontend cuando una slide no trae personalización.
--
-- Idempotente: || sobrescribe la clave "estilo" si ya existía.
-- ============================================================================

-- Slide 1 (contenido_texto, "¿Por qué este curso?"): degradado teal de marca.
UPDATE curso_slide s
SET configuracion_json = s.configuracion_json || '{
    "estilo": {
        "fondoClaro": "linear-gradient(135deg, #eaf7f2 0%, #bfe8d6 40%, #1f9d63 80%, #0f6e56 100%)",
        "fondoOscuro": "linear-gradient(135deg, #001a10 0%, #0a3d2a 50%, #0f6e56 100%)",
        "burbujas": true
    }
}'::jsonb
FROM curso c
WHERE c.id = s.curso_id
  AND c.titulo = 'Prevención de caídas'
  AND s.orden = 1;

-- Slide 2 (pregunta_vf, "arnés obligatorio..."): degradado azul institucional
-- (el mismo tono usado como ejemplo real en el encargo, "Escalera fija").
UPDATE curso_slide s
SET configuracion_json = s.configuracion_json || '{
    "estilo": {
        "fondoClaro": "linear-gradient(135deg, #eaf4fa 0%, #cbe7f5 40%, #208fcf 80%, #005d9d 100%)",
        "fondoOscuro": "linear-gradient(135deg, #000e1a 0%, #00345c 50%, #005d9d 100%)",
        "burbujas": true
    }
}'::jsonb
FROM curso c
WHERE c.id = s.curso_id
  AND c.titulo = 'Prevención de caídas'
  AND s.orden = 2;

-- Slide 4 (pregunta_imagen, "marca la imagen del arnés correcto"): degradado
-- de acento sin burbujas, para probar el caso "burbujas: false" (menos
-- distracción visual detrás de una imagen que hay que observar con detalle).
UPDATE curso_slide s
SET configuracion_json = s.configuracion_json || '{
    "estilo": {
        "fondoClaro": "linear-gradient(135deg, #fff7e6 0%, #ffe4b3 45%, #e6a532 100%)",
        "fondoOscuro": "linear-gradient(135deg, #1a1200 0%, #4d3400 50%, #7a5200 100%)",
        "burbujas": false
    }
}'::jsonb
FROM curso c
WHERE c.id = s.curso_id
  AND c.titulo = 'Prevención de caídas'
  AND s.orden = 4;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT s.orden, s.tipo_codigo, s.configuracion_json->'estilo' AS estilo
-- FROM curso_slide s
-- JOIN curso c ON c.id = s.curso_id
-- WHERE c.titulo = 'Prevención de caídas'
-- ORDER BY s.orden;
