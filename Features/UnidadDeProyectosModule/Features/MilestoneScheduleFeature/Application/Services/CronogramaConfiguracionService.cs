using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Services
{
    /// <summary>
    /// Configuración de los correos del Cronograma de Hitos. Lo propio de esta capa es dejar cada
    /// destinatario apuntando a una sola cosa (el trabajador, el rol o el correo escrito a mano) y
    /// traducir lo que devuelve la base a mensajes para la pantalla.
    /// </summary>
    public class CronogramaConfiguracionService : ICronogramaConfiguracionService
    {
        private const int CorreoMaximo = 200;
        private static readonly Regex CorreoValido = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly ICronogramaCorreosRepository _repo;

        public CronogramaConfiguracionService(ICronogramaCorreosRepository repo)
        {
            _repo = repo;
        }

        public Task<CronogramaConfiguracionDto> GetAsync() => _repo.GetConfiguracionAsync();

        public async Task SetCorreoActiveAsync(string codigo, bool active, int userId)
        {
            if (!await _repo.SetCorreoActiveAsync(codigo, active, userId))
                throw new AbrilException("El correo indicado no existe.", 404);
        }

        public async Task SetPrincipalActiveAsync(string codigo, bool active, int userId)
        {
            if (!await _repo.SetPrincipalActiveAsync(codigo, active, userId))
                throw new AbrilException("Ese correo no tiene un destinatario que ponga el sistema.", 404);
        }

        public async Task<CronogramaCorreoDto> CrearDestinatarioAsync(string codigo, CronogramaCorreoDestinatarioInputDto dto, int userId)
        {
            var d = Normalizar(dto);
            var resultado = await _repo.CrearDestinatarioAsync(codigo, d.Tipo, d.Recepcion, d.WorkerId, d.RoleId, d.Correo, userId);
            return Resolver(resultado, "El correo indicado no existe.");
        }

        public async Task<CronogramaCorreoDto> ActualizarDestinatarioAsync(int id, CronogramaCorreoDestinatarioInputDto dto, int userId)
        {
            var d = Normalizar(dto);
            var resultado = await _repo.ActualizarDestinatarioAsync(id, d.Tipo, d.Recepcion, d.WorkerId, d.RoleId, d.Correo, userId);
            return Resolver(resultado, "El destinatario indicado no existe.");
        }

        public async Task SetDestinatarioActiveAsync(int id, bool active, int userId)
        {
            if (!await _repo.SetDestinatarioActiveAsync(id, active, userId))
                throw new AbrilException("El destinatario indicado no existe.", 404);
        }

        public async Task<CronogramaCorreoDto> EliminarDestinatarioAsync(int id, int userId) =>
            await _repo.EliminarDestinatarioAsync(id, userId)
            ?? throw new AbrilException("El destinatario indicado no existe.", 404);

        /// <summary>
        /// Deja lleno solo el campo que corresponde al tipo (una fila con trabajador Y correo sería
        /// ambigua al enviar; el CHECK de la tabla también lo impide).
        /// </summary>
        private static (string Tipo, string Recepcion, int? WorkerId, int? RoleId, string? Correo) Normalizar(
            CronogramaCorreoDestinatarioInputDto? dto)
        {
            dto ??= new CronogramaCorreoDestinatarioInputDto();

            var tipo = (dto.TipoCodigo ?? string.Empty).Trim().ToUpperInvariant();
            if (!CronogramaCorreoDestinatarioTipos.Todos.Contains(tipo))
                throw new AbrilException("Elige a quién va: un trabajador, un rol o un correo.", 400);

            var recepcion = (dto.RecepcionCodigo ?? string.Empty).Trim().ToUpperInvariant();
            if (!CronogramaCorreoRecepciones.Todas.Contains(recepcion))
                throw new AbrilException("Elige cómo lo recibe: Para, CC o CCO.", 400);

            switch (tipo)
            {
                case CronogramaCorreoDestinatarioTipos.Trabajador:
                    if (dto.WorkerId is null or <= 0)
                        throw new AbrilException("Falta elegir el trabajador.", 400);
                    return (tipo, recepcion, dto.WorkerId, null, null);

                case CronogramaCorreoDestinatarioTipos.Rol:
                    if (dto.RoleId is null or <= 0)
                        throw new AbrilException("Falta elegir el rol.", 400);
                    return (tipo, recepcion, null, dto.RoleId, null);

                default:
                    var correo = (dto.Correo ?? string.Empty).Trim().ToLowerInvariant();
                    if (correo.Length == 0)
                        throw new AbrilException("Falta escribir el correo.", 400);
                    if (correo.Length > CorreoMaximo || !CorreoValido.IsMatch(correo))
                        throw new AbrilException($"Correo inválido: «{dto.Correo}».", 400);
                    return (tipo, recepcion, null, null, correo);
            }
        }

        private static CronogramaCorreoDto Resolver(CronogramaDestinatarioGuardadoDto resultado, string noEncontrado)
        {
            if (!resultado.Encontrado)
                throw new AbrilException(noEncontrado, 404);
            if (!resultado.CatalogoOk)
                throw new AbrilException("Falta la configuración base de los correos del cronograma. Contacta al administrador del sistema.", 500);
            if (!resultado.DestinoExiste)
                throw new AbrilException("El trabajador o el rol elegido ya no existe.", 400);
            if (!resultado.Guardado || resultado.Correo == null)
                throw new AbrilException("No se pudo guardar el destinatario.", 409);

            return resultado.Correo;
        }
    }
}
