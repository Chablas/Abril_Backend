using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Services
{
    public class RendicionService : IRendicionService
    {
        private readonly IRendicionRepository            _repo;
        private readonly IConsolidadoS10Service          _consolidadoService;
        private readonly IJefeRevisorResolver            _revisorResolver;
        private readonly ICorreoSalidaRecipientResolver  _correoResolver;
        private readonly IEmailService                   _emailService;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration                  _configuration;

        public RendicionService(
            IRendicionRepository repo,
            IConsolidadoS10Service consolidadoService,
            IJefeRevisorResolver revisorResolver,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration)
        {
            _repo               = repo;
            _consolidadoService = consolidadoService;
            _revisorResolver    = revisorResolver;
            _correoResolver     = correoResolver;
            _emailService       = emailService;
            _factory            = factory;
            _configuration      = configuration;
        }

        public async Task<RendicionListResultDto> GetByUserId(int userId, RendicionFiltersDto? filters = null)
        {
            // Las tarjetas se cuentan sobre lo mismo que muestra la tabla, así que salen de esta
            // lista: el listado no está paginado, ya viene entero.
            var data = await _repo.GetByUserId(userId, filters);
            return new RendicionListResultDto
            {
                Data    = data,
                Resumen = ResumenRendicionesDto.De(data),
            };
        }

        public async Task<RendicionFilterDataDto> GetFilterData(int userId) => new()
        {
            Periodos = await _repo.GetPeriodos(userId),
        };

        public async Task<RendicionDetalleDto> GetDetalle(int rendicionId, int userId) =>
            await _repo.GetDetalleForUser(rendicionId, userId)
                ?? throw new AbrilException("La planilla de rendición no existe o no es tuya.", 404);

        public Task<ConsolidadoS10Dto> UploadConsolidadoS10(int rendicionId, IFormFile file, int userId) =>
            // ownerUserId = userId: en el autoservicio la planilla tiene que incluir alguna salida
            // propia. El servicio compartido resuelve el resto (SharePoint, reemplazo, subsanación).
            _consolidadoService.UploadParaRendicion(rendicionId, file, userId, ownerUserId: userId);

        public async Task<string> NotificarRevisor(int rendicionId, int userId)
        {
            // El detalle ya trae el guard de propiedad, el estado del reembolso y el consolidado,
            // así que las validaciones se hacen contra él y no contra consultas sueltas.
            var planilla = await GetDetalle(rendicionId, userId);

            if (planilla.ConsolidadoS10 == null)
                throw new AbrilException(
                    "Primero adjunta el Consolidado del S10: es lo que el revisor tiene que mirar.", 400);

            if (!planilla.PuedeNotificarRevisor)
                throw new AbrilException(
                    "El reembolso de esta planilla ya fue revisado: no hace falta volver a avisar.", 400);

            using var ctx = _factory.CreateDbContext();

            var quien = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                where s.RendicionId == rendicionId && per.UserId == userId
                select new
                {
                    WorkerId    = w.Id,
                    SolicitudId = s.Id,
                    Trabajador  = per.FullName ?? "Trabajador",
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                }
            ).FirstOrDefaultAsync()
              ?? throw new AbrilException("No se pudo identificar al trabajador de la planilla.", 409);

            var revisor = await _revisorResolver.ResolveAsync(quien.WorkerId);
            if (string.IsNullOrWhiteSpace(revisor?.Email))
                throw new AbrilException(
                    "No se pudo determinar el correo de tu jefe/revisor. Avisa a Gestión del Talento Humano.", 409);

            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.S10Revisor,
                new List<string> { revisor!.Email! });

            if (!envio.Enviar)
                throw new AbrilException(
                    "El aviso al revisor está desactivado en la configuración de correos de Gestión Administrativa.",
                    409);

            var datos = new ReembolsoPlanillaCorreoDatos
            {
                RendicionId    = rendicionId,
                Trabajador     = quien.Trabajador,
                Area           = await ResolveAreaNombreAsync(ctx, quien.AreaScopeId),
                NumeroPlanilla = planilla.NumeroPlanilla,
                Periodo        = planilla.Periodo,
                SalidasCount   = planilla.SalidasCount,
                MontoTotal     = planilla.MontoTotal,
            };

            // El botón lleva a Gestión de Salidas, que es donde el revisor decide. Se abre en una
            // de las salidas de la planilla: desde ahí ve el resto con el filtro de la pantalla.
            var url  = SalidaEnlaces.Gestion(_configuration, quien.SolicitudId);
            var body = ReembolsoEmailTemplates.RevisionPendiente(
                SalidaEmailLayout.Desde(_configuration), datos, url);

            var asunto = planilla.NumeroPlanilla is null
                ? $"Reembolso por revisar - {quien.Trabajador} - {planilla.Periodo}"
                : $"Reembolso por revisar - {quien.Trabajador} - planilla {planilla.NumeroPlanilla}";

            await _emailService.SendAsync(
                to: envio.Para,
                subject: asunto,
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null);

            await _repo.MarcarRevisorNotificado(rendicionId, userId);

            var nombre = string.IsNullOrWhiteSpace(revisor.Nombre) ? "tu revisor" : revisor.Nombre;
            return $"Se le avisó a {nombre}.";
        }

        /// <summary>Nombre del área a la que apunta el nodo (el más bajo del árbol). Null si no tiene.</summary>
        private static async Task<string?> ResolveAreaNombreAsync(AppDbContext ctx, int? areaScopeId)
        {
            if (!areaScopeId.HasValue) return null;
            return await (
                from sc in ctx.AreaScope
                join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                where sc.AreaScopeId == areaScopeId.Value
                select it.AreaItemName
            ).FirstOrDefaultAsync();
        }
    }
}
