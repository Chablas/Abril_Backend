-- ============================================================================
-- Curso de ejemplo: "Prevención de caídas" — para probar el flujo completo del
-- CursoModule (curso-player) con los distintos tipos de slide interactiva.
--
-- Requiere haber corrido antes:
--   1) Migrations\Manual\20260922_CreateCursoModule.sql   (tablas)
--   2) Migrations_Manual\2026-09-22_cursos_feature_seed.sql (permisos)
--
-- Idempotente por titulo del curso (no duplica si se corre dos veces).
-- ============================================================================

DO $$
DECLARE
    v_curso_id integer;
BEGIN
    SELECT id INTO v_curso_id FROM curso WHERE titulo = 'Prevención de caídas';

    IF v_curso_id IS NULL THEN

        INSERT INTO curso (titulo, descripcion, categoria_nombre, rol_destino, nota_minima_aprobacion, activo)
        VALUES (
            'Prevención de caídas',
            'Curso corto sobre riesgos de caída en oficina y obra: identificación de peligros, uso de EPP y protocolos de trabajo seguro en altura.',
            'SSOMA',
            NULL, -- visible para todos los roles con acceso a la feature 'cursos.lista'
            70.00,
            true
        )
        RETURNING id INTO v_curso_id;

        -- 1) Contenido introductorio (no evaluable)
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 1, 'contenido_texto', false, NULL, NULL,
            '{
                "titulo": "¿Por qué este curso?",
                "texto": "Las caídas son una de las principales causas de accidentes tanto en oficina (pisos mojados, cables, escaleras) como en obra (andamios, aberturas, trabajos en altura). Este curso te enseña a identificar estos riesgos y a protegerte.",
                "imagenUrl": null
            }'::jsonb
        );

        -- 2) Verdadero / Falso
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 2, 'pregunta_vf', true, 20.00, 'igualdad_exacta',
            '{
                "enunciado": "Usar arnés es obligatorio solo si trabajas a más de 5 metros de altura.",
                "respuestaCorrecta": { "valor": false }
            }'::jsonb
        );

        -- 3) Opción múltiple
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 3, 'pregunta_opcion_multiple', true, 20.00, 'igualdad_exacta',
            '{
                "enunciado": "¿Cuál de estas opciones es la principal causa de caídas en oficina?",
                "opciones": [
                    { "id": "a", "texto": "Pisos mojados o cables sueltos" },
                    { "id": "b", "texto": "Uso de ascensor" },
                    { "id": "c", "texto": "Trabajar sentado" }
                ],
                "respuestaCorrecta": { "opcionId": "a" }
            }'::jsonb
        );

        -- 4) Marcar la imagen correcta
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 4, 'pregunta_imagen', true, 20.00, 'igualdad_exacta',
            '{
                "enunciado": "Marca la imagen que muestra el uso correcto del arnés de seguridad.",
                "imagenes": [
                    { "id": "img1", "texto": "Arnés bien ajustado con línea de vida anclada", "imagenUrl": "/assets/cursos/arnes-correcto.jpg" },
                    { "id": "img2", "texto": "Arnés suelto sin anclar", "imagenUrl": "/assets/cursos/arnes-incorrecto.jpg" }
                ],
                "respuestaCorrecta": { "imagenId": "img1" }
            }'::jsonb
        );

        -- 5) Arrastrar y soltar
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 5, 'pregunta_arrastrar', true, 20.00, 'igualdad_exacta',
            '{
                "enunciado": "Arrastra cada equipo de protección a la zona de trabajo donde corresponde usarlo.",
                "items": [
                    { "id": "item_arnes", "texto": "Arnés de seguridad" },
                    { "id": "item_zapatos", "texto": "Zapatos antideslizantes" }
                ],
                "zonas": [
                    { "id": "zona_altura", "texto": "Trabajo en altura (obra)" },
                    { "id": "zona_oficina", "texto": "Oficina" }
                ],
                "respuestaCorrecta": {
                    "asignaciones": {
                        "zona_altura": ["item_arnes"],
                        "zona_oficina": ["item_zapatos"]
                    }
                }
            }'::jsonb
        );

        -- 6) Ordenar pasos
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 6, 'pregunta_ordenar', true, 20.00, 'igualdad_exacta',
            '{
                "enunciado": "Ordena los pasos correctos para iniciar un trabajo en altura de forma segura.",
                "items": [
                    { "id": "paso1", "texto": "Inspeccionar el arnés y línea de vida" },
                    { "id": "paso2", "texto": "Anclar la línea de vida a un punto certificado" },
                    { "id": "paso3", "texto": "Verificar el permiso de trabajo en altura (PETAR)" },
                    { "id": "paso4", "texto": "Iniciar el ascenso" }
                ],
                "respuestaCorrecta": {
                    "ordenIds": ["paso3", "paso1", "paso2", "paso4"]
                }
            }'::jsonb
        );

    END IF;
END $$;

-- ============================================================================
-- Verificación (correr después; no modifica nada)
-- ============================================================================
-- SELECT s.orden, s.tipo_codigo, s.es_evaluable, s.puntaje
-- FROM curso_slide s
-- JOIN curso c ON c.id = s.curso_id
-- WHERE c.titulo = 'Prevención de caídas'
-- ORDER BY s.orden;
