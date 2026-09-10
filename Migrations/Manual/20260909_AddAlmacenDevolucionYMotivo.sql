-- Migración manual (pgAdmin) — módulo de Almacén/Logística.
-- Ejecutar directamente contra la BD PostgreSQL. No usar dotnet ef.
--
-- Agrega el tipo de movimiento "Devolucion" (devuelve stock por error de registro o por
-- sobrante encontrado en obra — se contabiliza en el saldo igual que un Ingreso) y su motivo.
-- No requiere CHECK constraint porque Tipo/MotivoDevolucion ya son TEXT libre validado en
-- código (ver TipoMovimientoAlmacen / MotivoDevolucion en AlmacenModels.cs).

ALTER TABLE almacen_movimientos
    ADD COLUMN IF NOT EXISTS motivo_devolucion TEXT;
