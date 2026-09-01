-- ═══════════════════════════════════════════════════════════════════════════
-- Carta oferta: generarla en el sistema desde una plantilla Word
-- ═══════════════════════════════════════════════════════════════════════════
--
-- Hasta ahora la carta oferta era SIEMPRE un PDF que GTH armaba por su cuenta y
-- subía. Ahora también se puede generar acá: se rellena una plantilla .docx con
-- placeholders ({{SUELDO}}, {{FECHA_INICIO_LABORES}}…), el .docx queda en el file
-- del colaborador para que GTH lo revise —y lo corrija en Word si hace falta— y
-- recién al enviarla se convierte a PDF, que es lo que ve y firma el candidato.
--
-- Eso parte el ciclo en dos momentos que antes eran uno solo, así que la fila de
-- gth_carta_oferta pasa a existir ANTES del envío (borrador): se crea al generar
-- el documento y se completa al enviarlo.
--
--   enviada_date_time IS NULL  → borrador: hay documento, todavía no se envió.
--   enviada_date_time NOT NULL → carta enviada (el comportamiento de siempre).
--
-- No hay estado nuevo en el requerimiento: generar el borrador NO mueve la fase.
-- La fase pasa a CARTA_OFERTA recién con el envío, igual que antes.
--
-- Las columnas van todas NULLABLE: las cartas ya enviadas nunca pasaron por la
-- generación y no hay valor que inventarles (ver la carta adjuntada a mano, que
-- sigue siendo una vía válida y tampoco las llena).
--
-- Idempotente: se puede correr más de una vez sin error.
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN;

ALTER TABLE gth_carta_oferta
    -- Condiciones de la propuesta que decide GTH y que la plantilla imprime. Se
    -- guardan porque son la propuesta en sí (el sueldo NO es el que puso el
    -- solicitante en el requerimiento: por regla de negocio lo pone GTH) y porque
    -- son lo que se vuelve a mostrar si hay que regenerar el documento.
    ADD COLUMN IF NOT EXISTS sueldo                  numeric(12,2)  NULL,
    ADD COLUMN IF NOT EXISTS fecha_limite_aceptacion date           NULL,

    -- El .docx generado, en el file del colaborador. Es el documento de trabajo:
    -- se puede regenerar y editar en SharePoint las veces que haga falta, y es de
    -- donde sale el PDF al enviar. Va aparte de carta_* (el PDF que se envió)
    -- porque son dos archivos distintos del mismo expediente.
    ADD COLUMN IF NOT EXISTS generada_nombre         varchar(300)   NULL,
    ADD COLUMN IF NOT EXISTS generada_url            text           NULL,
    ADD COLUMN IF NOT EXISTS generada_item_id        varchar(200)   NULL,
    ADD COLUMN IF NOT EXISTS generada_drive_id       varchar(200)   NULL,
    ADD COLUMN IF NOT EXISTS generada_date_time      timestamptz    NULL,
    ADD COLUMN IF NOT EXISTS generada_user_id        integer        NULL;

COMMENT ON COLUMN gth_carta_oferta.sueldo IS
    'Sueldo básico bruto mensual ofrecido, en soles. Lo pone GTH al generar la carta ({{SUELDO}} de la plantilla), NO sale del sueldo referencial del requerimiento. NULL en las cartas que se adjuntaron ya armadas.';

COMMENT ON COLUMN gth_carta_oferta.fecha_limite_aceptacion IS
    'Hasta cuándo el candidato puede aceptar la propuesta ({{FECHA_LIMITE_ACEPTACION}}). Por defecto el día siguiente al de la generación. NULL en las cartas adjuntadas ya armadas.';

COMMENT ON COLUMN gth_carta_oferta.generada_url IS
    'El .docx generado desde la plantilla, en la subcarpeta «Carta Oferta Enviada» del file del colaborador. Es el documento que GTH revisa (y puede corregir en Word) antes de enviar; el PDF que se manda sale de convertir ESTE archivo, no los bytes del momento de generarlo.';

COMMENT ON COLUMN gth_carta_oferta.generada_date_time IS
    'Última generación del .docx. Con esta columna llena y enviada_date_time en NULL, la carta es un borrador: existe el documento pero al candidato todavía no se le mandó nada.';

COMMIT;
