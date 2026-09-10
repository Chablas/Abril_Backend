using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Infrastructure.Interfaces
{
    public interface IRendicionRepository
    {
        /// <summary>
        /// Planillas que incluyen alguna salida del trabajador de ese usuario, ya filtradas. Los
        /// conteos y montos vienen acotados a sus propias salidas.
        /// </summary>
        Task<List<RendicionListItemDto>> GetByUserId(int userId, RendicionFiltersDto? filters = null);

        /// <summary>
        /// Una planilla con el desglose de las salidas propias del usuario. Null si la planilla no
        /// existe o no tiene ninguna salida suya (el guard de propiedad de esta pantalla).
        /// </summary>
        Task<RendicionDetalleDto?> GetDetalleForUser(int rendicionId, int userId);

        /// <summary>
        /// Meses con al menos una planilla propia, para el filtro de periodo, y la ficha del
        /// trabajador que se resolvió para hallarlos. El workerId se devuelve porque el servicio
        /// lo necesita para resolver el jefe/revisor y acá ya se consultó: pedirlo aparte sería un
        /// roundtrip más por la misma fila. Null cuando el usuario no tiene ficha de trabajador.
        /// </summary>
        Task<(int? WorkerId, List<PeriodoOptionDto> Periodos)> GetPeriodos(int userId);

        /// <summary>
        /// Marca que se le avisó al revisor por esta planilla. El sello queda en TODAS las salidas
        /// propias que cubre (la columna vive en la salida), porque el aviso es uno solo.
        /// </summary>
        Task MarcarRevisorNotificado(int rendicionId, int userId);

        /// <summary>
        /// Pasa la planilla a "En primera revisión" y estampa quién y cuándo la envió. El estado es
        /// de la planilla, así que se escribe una sola fila.
        /// </summary>
        Task MarcarEnviadaAPrimeraRevision(int rendicionId, int userId);

        /// <summary>
        /// Datos del trabajador dueño de las salidas propias de la planilla, para los correos:
        /// su ficha (para resolver el jefe), su nombre, su correo y el nombre de su área.
        /// Null si el usuario no tiene ninguna salida en esa planilla.
        /// </summary>
        Task<RendicionSolicitanteDto?> GetSolicitante(int rendicionId, int userId);

        /// <summary>Cuántos tramos (trayectos) suman las salidas propias de la planilla.</summary>
        Task<int> ContarTramos(int rendicionId, int userId);

        /// <summary>
        /// Registra la solicitud de corrección del Consolidado del S10 al Coordinador ERP (§10.5).
        /// Copia la observación de la jefatura y la guía del consolidado observado: el ERP las
        /// necesita y la observación de la salida se sobrescribe si la jefatura vuelve a observar.
        ///
        /// No mueve el estado del reembolso: la salida se queda Observada mientras dura la gestión.
        /// Lo que cambia es de quién es la pelota, y eso vive en la propia corrección.
        /// </summary>
        /// <returns>La corrección recién creada, ya lista para exponer.</returns>
        Task<CorreccionS10Dto> CrearCorreccion(int rendicionId, string motivo, int userId);

        /// <summary>
        /// Correos de los usuarios con el rol COORDINADOR ERP: el destinatario principal de la
        /// solicitud de corrección. Se resuelve por ROL y no por área porque el responsable ERP no
        /// cuelga del organigrama del solicitante — mismo criterio que el aviso a Tesorería.
        /// </summary>
        Task<List<string>> GetCorreosCoordinadorErp();
    }
}
