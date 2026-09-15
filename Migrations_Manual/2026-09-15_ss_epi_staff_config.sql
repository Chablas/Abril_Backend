-- Configuración global (una sola fila, para toda la empresa, no por proyecto) de cuánto EPI
-- compartido con obrero (arnés/lentes/barbiquejo/guantes) rota por miembro de Staff. Se usa para
-- descontar la porción de Staff del total consumido/calculado de esas 4 families antes de
-- mostrarlas como "EPI Obrero" en el Desagregado de Recursos — evita duplicar el costo entre las
-- líneas de Staff y Obrero cuando ambos comparten el mismo SKU.
--
-- rotacion_*_meses = cada cuántos meses se repone 1 unidad por persona (lentes=1 → 1 por mes;
-- barbiquejo=3 → 1 cada 3 meses; guantes=2 → 1 cada 2 meses). Arnés no tiene componente de
-- tiempo — se entrega una sola vez por miembro de staff, igual que casco/orejera.

CREATE TABLE IF NOT EXISTS ss_epi_staff_config (
    id                        SERIAL PRIMARY KEY,
    arnes_por_staff           NUMERIC(6,2) NOT NULL DEFAULT 1,
    rotacion_lentes_meses     NUMERIC(6,2) NOT NULL DEFAULT 1,
    rotacion_barbiquejo_meses NUMERIC(6,2) NOT NULL DEFAULT 3,
    rotacion_guantes_meses    NUMERIC(6,2) NOT NULL DEFAULT 2,
    actualizado_en            TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO ss_epi_staff_config (arnes_por_staff, rotacion_lentes_meses, rotacion_barbiquejo_meses, rotacion_guantes_meses)
SELECT 1, 1, 3, 2
WHERE NOT EXISTS (SELECT 1 FROM ss_epi_staff_config);
