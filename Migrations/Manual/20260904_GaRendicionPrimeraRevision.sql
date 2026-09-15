-- ============================================================================
-- Gestión Administrativa · Salidas — Primera revisión de la rendición
-- Fecha: 2026-09-04
--
-- Hasta hoy una planilla recién rendida iba derecho al Consolidado del S10 y la
-- única revisión del jefe era la del reembolso (la SEGUNDA del requerimiento
-- funcional). Ahora la rendición pasa antes por una PRIMERA revisión: el
-- trabajador la envía, el jefe mira tramos, montos y capturas, y recién con su
-- aprobación se habilita el Consolidado del S10 (RG-30 y RG-35). Si la observa,
-- el trabajador corrige capturas y montos de las salidas de esa planilla y
-- vuelve a generar el PDF conservando el mismo código (RG-02).
--
-- Lo que agrega este script:
--   1) ga_estado_primera_revision  → catálogo del nuevo eje de estado
--   2) ga_rendicion                → codigo REN-AAAA-NNNN + el eje de primera
--                                    revisión (estado, sellos y observación)
--   3) backfill                    → las planillas que ya existen nacen
--                                    APROBADAS y con su código
--   4) ga_solicitud_captura.state  → soft delete, para poder QUITAR una captura
--                                    al subsanar (antes solo se podían agregar)
--   5) ga_correo_evento            → los cuatro correos nuevos del flujo
--
-- El eje es de la PLANILLA y no de la salida, igual que el Consolidado del S10 y
-- la firma: el documento que el jefe revisa cubre todas las salidas que agrupa.
--
-- Idempotente: se puede correr varias veces. Aplicar en dev y prod.
-- ============================================================================

BEGIN;

-- ============================================================================
-- 1) Catálogo del estado de la primera revisión
--
--    Mismo shape que ga_estado_reembolso (id fijo, descripcion, orden, activo):
--    la lógica compara por id contra las constantes de EstadosSalida y el nombre
--    solo se expone en los DTOs.
--
--    BORRADOR es el estado en el que nace una planilla al rendirse: el PDF ya
--    está, pero el trabajador todavía no lo envió. Es el "Lista para enviar" del
--    requerimiento funcional.
-- ============================================================================
CREATE TABLE IF NOT EXISTS ga_estado_primera_revision (
    id          integer PRIMARY KEY,
    descripcion varchar(50) NOT NULL,
    orden       integer     NOT NULL DEFAULT 0,
    activo      boolean     NOT NULL DEFAULT true
);

INSERT INTO ga_estado_primera_revision (id, descripcion, orden) VALUES
    (1, 'Lista para enviar',   1),
    (2, 'En primera revisión', 2),
    (3, 'Aprobada',            3),
    (4, 'Observada',           4)
ON CONFLICT (id) DO NOTHING;

-- ============================================================================
-- 2) ga_rendicion — código propio y el eje de primera revisión
--
--    codigo/anio/numero replican lo que ya hace ga_solicitud_salida con su
--    SOL-AAAA-NNNN: el correlativo se reinicia con el año y el código es lo que
--    ve el trabajador (pantallas y correos). Es la pieza que hace posible
--    "regenerar el archivo con el mismo código": el PDF se reemplaza, la fila
--    (y con ella el código) es la misma.
--
--    numero_planilla NO se toca: sigue siendo el correlativo que se imprime en
--    el PDF ("TI: 000123"), que es un dato del papel y no el identificador de la
--    rendición.
-- ============================================================================
ALTER TABLE ga_rendicion
    ADD COLUMN IF NOT EXISTS codigo text,
    ADD COLUMN IF NOT EXISTS anio   integer,
    ADD COLUMN IF NOT EXISTS numero integer,
    ADD COLUMN IF NOT EXISTS estado_primera_revision_id   integer,
    ADD COLUMN IF NOT EXISTS enviada_revision_at          timestamptz,
    ADD COLUMN IF NOT EXISTS enviada_revision_por_id      integer,
    ADD COLUMN IF NOT EXISTS primera_revision_at          timestamptz,
    ADD COLUMN IF NOT EXISTS primera_revision_por_id      integer,
    ADD COLUMN IF NOT EXISTS primera_revision_observacion text;

-- ============================================================================
-- 3) Backfill — antes de los NOT NULL y los índices únicos
--
--    3.a) Estado: todo lo ya rendido nace APROBADO (3). Es lo que refleja la
--         realidad: esas planillas ya pasaron a Consolidado del S10 con las
--         reglas viejas, y meterlas en "Lista para enviar" les escondería el
--         botón del S10 a trabajadores que están a mitad del trámite.
-- ============================================================================
UPDATE ga_rendicion
   SET estado_primera_revision_id = 3
 WHERE estado_primera_revision_id IS NULL;

ALTER TABLE ga_rendicion
    ALTER COLUMN estado_primera_revision_id SET NOT NULL,
    ALTER COLUMN estado_primera_revision_id SET DEFAULT 1;

-- 3.b) Código: por año de rendido_at y en orden de rendición, para que el
--      correlativo siga la misma cronología que tendría si hubiera existido
--      desde el principio.
WITH numerado AS (
    SELECT id,
           EXTRACT(YEAR FROM rendido_at AT TIME ZONE 'America/Lima')::int AS anio_calc,
           ROW_NUMBER() OVER (
               PARTITION BY EXTRACT(YEAR FROM rendido_at AT TIME ZONE 'America/Lima')::int
               ORDER BY rendido_at, id
           )::int AS numero_calc
      FROM ga_rendicion
     WHERE codigo IS NULL
)
UPDATE ga_rendicion r
   SET anio   = n.anio_calc,
       numero = n.numero_calc,
       codigo = 'REN-' || n.anio_calc || '-' || lpad(n.numero_calc::text, 4, '0')
  FROM numerado n
 WHERE r.id = n.id;

-- ============================================================================
-- 4) Restricciones e índices de ga_rendicion
--
--    El único parcial (anio IS NOT NULL) es por si alguna fila quedara sin
--    numerar: un NULL no puede bloquear a otro, pero el índice tampoco tiene
--    que dejar pasar dos veces el mismo número del mismo año.
-- ============================================================================
CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_rendicion_codigo
    ON ga_rendicion (codigo) WHERE codigo IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_ga_rendicion_anio_numero
    ON ga_rendicion (anio, numero) WHERE anio IS NOT NULL AND numero IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_ga_rendicion_estado_primera_revision
    ON ga_rendicion (estado_primera_revision_id);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_rendicion_estado_primera_revision'
    ) THEN
        ALTER TABLE ga_rendicion
            ADD CONSTRAINT fk_ga_rendicion_estado_primera_revision
            FOREIGN KEY (estado_primera_revision_id) REFERENCES ga_estado_primera_revision(id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_rendicion_enviada_revision_por'
    ) THEN
        ALTER TABLE ga_rendicion
            ADD CONSTRAINT fk_ga_rendicion_enviada_revision_por
            FOREIGN KEY (enviada_revision_por_id) REFERENCES app_user(user_id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_ga_rendicion_primera_revision_por'
    ) THEN
        ALTER TABLE ga_rendicion
            ADD CONSTRAINT fk_ga_rendicion_primera_revision_por
            FOREIGN KEY (primera_revision_por_id) REFERENCES app_user(user_id);
    END IF;
END $$;

-- ============================================================================
-- 5) ga_solicitud_captura.state — soft delete
--
--    Subsanar una rendición observada es corregir capturas Y montos, y a veces
--    lo que hay que corregir es que sobra una captura. Hasta ahora la tabla solo
--    se insertaba, así que no había forma de quitarla. state = false es
--    "eliminada": ninguna consulta la vuelve a mostrar ni la suma al importe,
--    pero la fila queda para auditoría.
-- ============================================================================
ALTER TABLE ga_solicitud_captura
    ADD COLUMN IF NOT EXISTS state boolean NOT NULL DEFAULT true;

CREATE INDEX IF NOT EXISTS ix_ga_solicitud_captura_trayecto_state
    ON ga_solicitud_captura (trayecto_id) WHERE state;

-- ============================================================================
-- 6) Los cuatro correos nuevos del flujo de la primera revisión
--
--    Se administran solos desde Gestión Administrativa → Configuración →
--    Correos: esa pantalla lista ga_correo_evento tal cual, así que agregar la
--    fila alcanza (el frontend no se toca).
--
--    Los cuatro nacen PRENDIDOS y con el destinatario principal prendido: son
--    los avisos que sostienen el flujo nuevo, y apagarlos dejaría al jefe sin
--    saber que tiene algo por revisar. El interruptor queda disponible igual.
--
--    ⚠️ Sin correr el script los correos salen igual: cuando el evento no existe
--       en la tabla, CorreoSalidaRecipientResolver cae en su default (se envía al
--       principal, sin configurados). Lo único que falta hasta correrlo son las
--       pestañas de la pantalla.
-- ============================================================================
INSERT INTO ga_correo_evento (
    codigo, nombre, descripcion, orden, active,
    destinatario_principal_nombre, destinatario_principal_activo,
    permite_desactivar_envio, permite_desactivar_principal
)
SELECT v.codigo, v.nombre, v.descripcion, v.orden, true,
       v.principal, true, true, true
FROM (VALUES
    ('REN_PRIMERA_REVISION',
     'Rendición por revisar · al revisor',
     'Le avisa al jefe/revisor que el trabajador envió una rendición a primera revisión. Lleva los dos botones para entrar a aprobarla u observarla.',
     5,
     'El jefe/revisor del solicitante'),
    ('REN_ENVIADA',
     'Rendición enviada · al solicitante',
     'Confirma al trabajador que su rendición quedó registrada y a quién se le envió para la primera revisión.',
     6,
     'El solicitante'),
    ('REN_PRIMERA_APROBADA',
     'Primera revisión aprobada · al solicitante',
     'Le avisa al trabajador que su jefe aprobó la primera revisión y que ya puede cargar el Consolidado del S10.',
     7,
     'El solicitante'),
    ('REN_PRIMERA_OBSERVADA',
     'Rendición observada · al solicitante',
     'Le avisa al trabajador que su jefe observó la rendición, con el comentario de qué corregir antes de volver a generarla.',
     8,
     'El solicitante')
) AS v(codigo, nombre, descripcion, orden, principal)
WHERE NOT EXISTS (
    SELECT 1 FROM ga_correo_evento e WHERE e.codigo = v.codigo AND e.state
);

-- Los correos del S10 y del reembolso pasan a ir DESPUÉS de la primera revisión:
-- `orden` es solo el orden de las pestañas, y el flujo real hoy es
-- rendir → primera revisión → S10 → segunda revisión.
UPDATE ga_correo_evento SET orden = 9  WHERE codigo = 'S10_REVISOR'         AND orden <> 9;
UPDATE ga_correo_evento SET orden = 10 WHERE codigo = 'REEMBOLSO_APROBADO'  AND orden <> 10;
UPDATE ga_correo_evento SET orden = 11 WHERE codigo = 'REEMBOLSO_RECHAZADO' AND orden <> 11;

COMMIT;

-- ============================================================================
-- Verificación
-- ============================================================================
-- Estados del catálogo nuevo (4 filas):
-- SELECT * FROM ga_estado_primera_revision ORDER BY orden;
--
-- Las planillas con su código y su estado (todo lo previo al script debe salir
-- en 3 = Aprobada, y ninguna sin código):
-- SELECT id, codigo, anio, numero, numero_planilla, estado_primera_revision_id,
--        rendido_at
--   FROM ga_rendicion ORDER BY rendido_at DESC LIMIT 20;
-- SELECT count(*) AS sin_codigo FROM ga_rendicion WHERE codigo IS NULL;
--
-- Las pestañas de Configuración → Correos en su orden final (11 filas: 1-4
-- solicitud, 5-8 primera revisión, 9-11 S10 y reembolso):
-- SELECT codigo, nombre, orden, active, destinatario_principal_nombre
--   FROM ga_correo_evento WHERE state ORDER BY orden, id;
--
-- La columna de soft delete de capturas (ninguna eliminada todavía):
-- SELECT count(*) FILTER (WHERE state) AS vigentes,
--        count(*) FILTER (WHERE NOT state) AS eliminadas
--   FROM ga_solicitud_captura;
