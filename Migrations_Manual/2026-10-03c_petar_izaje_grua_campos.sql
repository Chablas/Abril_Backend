-- ============================================================================
-- PETAR — SSO-FO-043 (Izaje con grúa) tiene campos técnicos de formulario
-- (tipo/modelo/capacidad de la grúa, ángulo de pluma, peso de la carga, etc.)
-- que NO son ítems de checklist SI/NO y por lo tanto no cabían en
-- ss_petar_item. Van en tabla aparte, 1:1 con ss_petar, porque solo aplican
-- al tipo "Izaje de cargas" — el resto de tipos no tiene fila acá.
--
-- Idempotente.
-- ============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS ss_petar_izaje_grua (
    petar_id                     int PRIMARY KEY REFERENCES ss_petar(id),
    tipo_grua                    varchar(20), -- 'TorreGrua' | 'GruaMovil'
    fabricante_o_marca           text,
    modelo_o_placa                text,
    serie_o_tarjeta_circulacion  text,
    longitud_pluma_brazo_m       numeric(6,2),
    radio_maximo_giro_m          numeric(6,2),
    direccion_grado_giro         text,
    elevacion_m                  numeric(6,2),
    angulo_pluma                 numeric(5,2),
    capacidad_certificada_ton    numeric(8,2),
    peso_carga_total_ton         numeric(8,2),
    porcentaje_capacidad         numeric(5,2),
    tamano_estrobo               text,
    observaciones                text
);

COMMIT;

-- Verificación:
-- SELECT * FROM ss_petar_izaje_grua LIMIT 5;
