-- Tabla independiente para correos del área de Costos y Presupuestos.
-- Reemplaza las listas hardcodeadas en ContractorRegistrationService y ProjectSubContractorService.
CREATE TABLE CostosPresupuestosEmail (
    CostosPresupuestosEmailId INT IDENTITY(1,1) PRIMARY KEY,
    Email                     NVARCHAR(200)     NOT NULL,
    Active                    BIT               NOT NULL DEFAULT 1,
    State                     BIT               NOT NULL DEFAULT 1,
    CreatedDateTime           DATETIMEOFFSET    NOT NULL,
    CreatedUserId             INT               NOT NULL,
    UpdatedDateTime           DATETIMEOFFSET    NULL,
    UpdatedUserId             INT               NULL
);

-- Datos iniciales (ajustar correos reales antes de ejecutar en producción)
-- INSERT INTO CostosPresupuestosEmail (Email, Active, State, CreatedDateTime, CreatedUserId)
-- VALUES
--     ('eaguinaga@abril.pe',  1, 1, SYSDATETIMEOFFSET(), 1),
--     ('apimentel@abril.pe',  1, 1, SYSDATETIMEOFFSET(), 1),
--     ('bquicana@abril.pe',   1, 1, SYSDATETIMEOFFSET(), 1),
--     ('cavila@abril.pe',     1, 1, SYSDATETIMEOFFSET(), 1);
