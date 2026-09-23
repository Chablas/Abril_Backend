-- ============================================================================
-- Curso: "Caída de escalera fija" — riesgo de caída al mismo/distinto nivel al
-- usar escaleras fijas, aplicable tanto en oficina como en obra de construcción.
--
-- Requiere haber corrido antes:
--   1) Migrations\Manual\20260922_CreateCursoModule.sql   (tablas)
--   2) Migrations_Manual\2026-09-22_cursos_feature_seed.sql (permisos)
--
-- Formato calcado de 2026-09-22_curso_ejemplo_prevencion_caidas.sql (mismas
-- columnas de curso/curso_slide, shape de configuracion_json por tipo de
-- slide y convención de respuestaCorrecta). Incluye además, dentro de
-- configuracion_json, los campos opcionales "kicker" e "iconoDecorativo"
-- (nuevos, en desarrollo en paralelo en el frontend) y bloques "estilo"
-- (fondoClaro/fondoOscuro), siguiendo la convención usada en
-- 2026-09-22_curso_ejemplo_estilos_update.sql, en solo 3 de las 9 slides
-- para probar también el fallback visual por defecto en el resto.
--
-- Idempotente por titulo del curso (no duplica si se corre dos veces).
-- ============================================================================

DO $$
DECLARE
    v_curso_id integer;
BEGIN
    SELECT id INTO v_curso_id FROM curso WHERE titulo = 'Caída de escalera fija';

    IF v_curso_id IS NULL THEN

        INSERT INTO curso (titulo, descripcion, categoria_nombre, rol_destino, nota_minima_aprobacion, activo)
        VALUES (
            'Caída de escalera fija',
            'Las escaleras fijas se usan a diario en oficina y obra, y una mala costumbre basta para provocar una caída grave. Aprende a reconocer los riesgos, aplicar la línea de tres puntos de apoyo y usar las medidas de prevención antes de subir o bajar.',
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
                "kicker": "Introducción",
                "iconoDecorativo": "ti-stairs",
                "titulo": "Un riesgo que subestimamos todos los días",
                "texto": "Las caídas en escaleras fijas son una de las causas más frecuentes de accidentes, tanto en oficina (escaleras de emergencia, escaleras entre pisos) como en obra (escaleras de acceso a plataformas y andamios). La mayoría ocurre por distracción, prisa o malos hábitos, no por fallas del equipo. Este curso te enseña a subir y bajar de forma segura.",
                "imagenUrl": null,
                "estilo": {
                    "fondoClaro": "linear-gradient(135deg, #eaf4fa 0%, #cbe7f5 40%, #208fcf 80%, #005d9d 100%)",
                    "fondoOscuro": "linear-gradient(135deg, #000e1a 0%, #00345c 50%, #005d9d 100%)",
                    "burbujas": true
                }
            }'::jsonb
        );

        -- 2) Contenido: causas comunes de caída
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 2, 'contenido_texto', false, NULL, NULL,
            '{
                "kicker": "Causas comunes",
                "iconoDecorativo": "ti-alert-triangle",
                "titulo": "¿Por qué nos caemos en una escalera fija?",
                "texto": "Las causas más frecuentes son: bajar mirando el celular o de espaldas, llevar cajas u objetos con ambas manos (sin poder sujetarse), correr o saltar escalones, peldaños sueltos o desgastados, ausencia de barandas, mala iluminación y usar calzado inadecuado (suela lisa, taco alto). Reconocer estas causas es el primer paso para evitarlas.",
                "imagenUrl": null
            }'::jsonb
        );

        -- 3) V/F sobre la regla de los tres puntos de apoyo
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 3, 'pregunta_vf', true, 12.50, 'igualdad_exacta',
            '{
                "enunciado": "La regla de los tres puntos de apoyo significa mantener siempre 3 de las 4 extremidades (2 manos y 2 pies) en contacto con la escalera al subir o bajar.",
                "respuestaCorrecta": { "valor": true }
            }'::jsonb
        );

        -- 4) Contenido: cómo prevenir (barandas, superficie, señalización)
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 4, 'contenido_texto', false, NULL, NULL,
            '{
                "kicker": "Cómo prevenir",
                "iconoDecorativo": "ti-shield-check",
                "titulo": "Medidas que sí funcionan",
                "texto": "Antes de usar una escalera fija: inspecciónala (peldaños, barandas, superficie antideslizante), asegúrate de que esté bien iluminada y señalizada, sujétate siempre del pasamanos y nunca cargues objetos con ambas manos. En obra, verifica además que la escalera esté anclada y cumpla con el ángulo de inclinación normado.",
                "imagenUrl": null,
                "estilo": {
                    "fondoClaro": "linear-gradient(135deg, #eaf7f2 0%, #bfe8d6 40%, #1f9d63 80%, #0f6e56 100%)",
                    "fondoOscuro": "linear-gradient(135deg, #001a10 0%, #0a3d2a 50%, #0f6e56 100%)",
                    "burbujas": true
                }
            }'::jsonb
        );

        -- 5) Opción múltiple: ángulo de inclinación recomendado
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 5, 'pregunta_opcion_multiple', true, 12.50, 'igualdad_exacta',
            '{
                "enunciado": "¿Cuál es el ángulo de inclinación recomendado (relación 4:1) para una escalera fija o de mano apoyada contra una superficie?",
                "opciones": [
                    { "id": "a", "texto": "Aproximadamente 75° respecto al piso" },
                    { "id": "b", "texto": "Aproximadamente 30° respecto al piso" },
                    { "id": "c", "texto": "90°, totalmente vertical" }
                ],
                "respuestaCorrecta": { "opcionId": "a" }
            }'::jsonb
        );

        -- 6) Marcar la imagen correcta
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 6, 'pregunta_imagen', true, 12.50, 'igualdad_exacta',
            '{
                "enunciado": "Marca la imagen que muestra el uso correcto de la escalera fija.",
                "imagenes": [
                    { "id": "img1", "texto": "Sujeto del pasamanos, manos libres, mirando los escalones", "imagenUrl": "/assets/cursos/escalera-correcta.jpg" },
                    { "id": "img2", "texto": "Bajando con una caja en ambas manos, sin sujetarse", "imagenUrl": "/assets/cursos/escalera-incorrecta.jpg" }
                ],
                "respuestaCorrecta": { "imagenId": "img1" },
                "estilo": {
                    "fondoClaro": "linear-gradient(135deg, #fff7e6 0%, #ffe4b3 45%, #e6a532 100%)",
                    "fondoOscuro": "linear-gradient(135deg, #1a1200 0%, #4d3400 50%, #7a5200 100%)",
                    "burbujas": false
                }
            }'::jsonb
        );

        -- 7) Arrastrar: relacionar causa de caída con medida de prevención
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 7, 'pregunta_arrastrar', true, 12.50, 'igualdad_exacta',
            '{
                "enunciado": "Arrastra cada causa de caída hacia la medida de prevención que la corrige.",
                "items": [
                    { "id": "item_manos_ocupadas", "texto": "Bajar con las manos ocupadas" },
                    { "id": "item_piso_oscuro", "texto": "Escalera mal iluminada" }
                ],
                "zonas": [
                    { "id": "zona_tres_puntos", "texto": "Usar la línea de tres puntos de apoyo" },
                    { "id": "zona_iluminacion", "texto": "Instalar o reparar la iluminación" }
                ],
                "respuestaCorrecta": {
                    "asignaciones": {
                        "zona_tres_puntos": ["item_manos_ocupadas"],
                        "zona_iluminacion": ["item_piso_oscuro"]
                    }
                }
            }'::jsonb
        );

        -- 8) Ordenar pasos para subir/bajar de forma segura
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 8, 'pregunta_ordenar', true, 12.50, 'igualdad_exacta',
            '{
                "enunciado": "Ordena los pasos correctos para subir o bajar una escalera fija de forma segura.",
                "items": [
                    { "id": "paso1", "texto": "Inspeccionar visualmente la escalera (peldaños, barandas, superficie)" },
                    { "id": "paso2", "texto": "Liberar las manos: no cargar objetos que impidan sujetarse" },
                    { "id": "paso3", "texto": "Sujetarse del pasamanos y mantener la línea de tres puntos de apoyo" },
                    { "id": "paso4", "texto": "Subir o bajar mirando los escalones, sin correr" }
                ],
                "respuestaCorrecta": {
                    "ordenIds": ["paso1", "paso2", "paso3", "paso4"]
                }
            }'::jsonb
        );

        -- 9) V/F de cierre: calzado adecuado
        INSERT INTO curso_slide (curso_id, orden, tipo_codigo, es_evaluable, puntaje, modo_correccion, configuracion_json)
        VALUES (
            v_curso_id, 9, 'pregunta_vf', true, 12.50, 'igualdad_exacta',
            '{
                "kicker": "Repaso final",
                "iconoDecorativo": "ti-shoe",
                "enunciado": "El calzado con suela antideslizante en buen estado es una medida de prevención tan importante como sujetarse del pasamanos.",
                "respuestaCorrecta": { "valor": true }
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
-- WHERE c.titulo = 'Caída de escalera fija'
-- ORDER BY s.orden;
