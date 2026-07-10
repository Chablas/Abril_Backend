using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class InterconsultaService : IInterconsultaService
    {
        private static readonly HashSet<string> EstadosValidos = new() { "Pendiente", "Atendida", "Cancelada" };
        private static readonly HashSet<string> ObraOficinaConCorreoPropio = new(StringComparer.OrdinalIgnoreCase) { "Staff", "Oficina Central" };

        /// <summary>Remitente para todos los correos de Salud Ocupacional (Interconsultas, EMO, etc.).</summary>
        private const string RemitenteSaludOcupacional = "medicinaocupacionalnm@abril.pe";

        private readonly IInterconsultaRepository _repo;
        private readonly IEmailService _emailService;

        public InterconsultaService(IInterconsultaRepository repo, IEmailService emailService)
        {
            _repo = repo;
            _emailService = emailService;
        }

        public Task<PagedResult<InterconsultaListDto>> List(InterconsultaFilterDto filter) => _repo.List(filter);

        public async Task<InterconsultaEnviarCorreoResultDto> EnviarRecordatorios(List<int> ids)
        {
            var result = new InterconsultaEnviarCorreoResultDto();
            if (ids == null || ids.Count == 0)
                throw new AbrilException("Debe seleccionar al menos una interconsulta.", 400);

            var info = await _repo.GetForEnvioCorreo(ids);
            result.TotalSeleccionadas = info.Count;
            if (info.Count == 0)
                throw new AbrilException("No se encontraron interconsultas para las seleccionadas.", 404);

            // Staff/Oficina Central con correo corporativo propio → correo individual a él + su jefatura de proyecto.
            var conCorreoPropio = info
                .Where(x => x.TieneCorreoPropio && ObraOficinaConCorreoPropio.Contains(x.ObraOficina ?? string.Empty))
                .ToList();

            // Obra/Contratista (u otros sin correo propio) → se agrupan por proyecto en un solo
            // correo consolidado al administrador encargado, porque el trabajador no tiene email.
            var sinCorreoPropio = info.Except(conCorreoPropio).ToList();

            foreach (var item in conCorreoPropio)
            {
                // Oficina Central no tiene proyecto: se notifica a su jefatura + admin de la empresa.
                // Staff sí está asignado a un proyecto real, se notifica a la jefatura de ese proyecto.
                var destinatariosRaw = item.EsOficinaCentral
                    ? new List<string?> { item.WorkerEmailCorporativo, item.JefaturaEmail, item.ContributorEmailAdministrador }
                    : new List<string?>
                    {
                        item.WorkerEmailCorporativo,
                        item.ProyectoEmailResidente,
                        item.ProyectoEmailResponsable,
                        item.ProyectoEmailRrhh,
                        item.ProyectoEmailCoordSsoma,
                        item.ProyectoEmailCoordAdmin
                    };

                var destinatarios = destinatariosRaw
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

                if (destinatarios.Count == 0)
                {
                    result.TotalErrores++;
                    result.Detalles.Add($"Interconsulta {item.Id} ({item.WorkerNombre}) — sin destinatarios. Omitida.");
                    continue;
                }

                try
                {
                    await _emailService.SendAsync(
                        to: destinatarios,
                        subject: $"Recordatorio de interconsulta pendiente - {item.WorkerNombre}",
                        body: BuildBodyIndividual(item),
                        isHtml: true,
                        fromOverride: RemitenteSaludOcupacional);

                    result.TotalEnviados++;
                    result.Detalles.Add($"Interconsulta {item.Id} ({item.WorkerNombre}) — enviado a {destinatarios.Count} destinatario(s).");
                }
                catch (Exception ex)
                {
                    result.TotalErrores++;
                    result.Detalles.Add($"Interconsulta {item.Id} ({item.WorkerNombre}) — error al enviar: {ex.Message}");
                }
            }

            var gruposPorProyecto = sinCorreoPropio
                .GroupBy(x => new { x.ProyectoId, x.ProyectoNombre, x.ContributorNombre, Admin = x.ProyectoEmailCoordAdmin ?? x.ContributorEmailAdministrador });

            foreach (var grupo in gruposPorProyecto)
            {
                var admin = grupo.Key.Admin;
                var proyectoNombre = grupo.Key.ProyectoNombre ?? "Sin proyecto asignado";

                if (string.IsNullOrWhiteSpace(admin))
                {
                    result.TotalErrores += grupo.Count();
                    foreach (var it in grupo)
                        result.Detalles.Add($"Interconsulta {it.Id} ({it.WorkerNombre}) — sin administrador encargado en '{proyectoNombre}'. Omitida.");
                    continue;
                }

                try
                {
                    await _emailService.SendAsync(
                        to: new List<string> { admin.Trim() },
                        subject: $"Interconsultas pendientes - {proyectoNombre}",
                        body: BuildBodyConsolidado(proyectoNombre, grupo.Key.ContributorNombre, grupo.ToList()),
                        isHtml: true,
                        fromOverride: RemitenteSaludOcupacional);

                    result.TotalEnviados += grupo.Count();
                    result.Detalles.Add($"{proyectoNombre} — enviado a {admin} ({grupo.Count()} trabajador(es) sin correo propio).");
                }
                catch (Exception ex)
                {
                    result.TotalErrores += grupo.Count();
                    result.Detalles.Add($"{proyectoNombre} — error al enviar a {admin}: {ex.Message}");
                }
            }

            return result;
        }

        private static string BuildBodyIndividual(InterconsultaEnvioInfoDto item)
        {
            var ubicacionLabel = item.EsOficinaCentral ? "Jefatura" : "Proyecto";
            var ubicacionValor = item.EsOficinaCentral ? (item.Jefatura ?? "Oficina Central") : (item.ProyectoNombre ?? "—");

            return $@"
            <p>Estimados,</p>
            <p>Se recuerda que el siguiente trabajador tiene una <strong>interconsulta pendiente</strong>:</p>
            <table style='border-collapse: collapse; font-family: Arial, sans-serif; font-size: 14px;'>
                <tr><td style='border: 1px solid #ddd; padding: 8px;'><strong>Trabajador</strong></td><td style='border: 1px solid #ddd; padding: 8px;'>{item.WorkerNombre}</td></tr>
                <tr><td style='border: 1px solid #ddd; padding: 8px;'><strong>DNI</strong></td><td style='border: 1px solid #ddd; padding: 8px;'>{item.WorkerDni}</td></tr>
                <tr><td style='border: 1px solid #ddd; padding: 8px;'><strong>Especialidad</strong></td><td style='border: 1px solid #ddd; padding: 8px;'>{item.Especialidad}</td></tr>
                <tr><td style='border: 1px solid #ddd; padding: 8px;'><strong>{ubicacionLabel}</strong></td><td style='border: 1px solid #ddd; padding: 8px;'>{ubicacionValor}</td></tr>
                <tr><td style='border: 1px solid #ddd; padding: 8px;'><strong>Fecha de derivación</strong></td><td style='border: 1px solid #ddd; padding: 8px;'>{item.FechaDerivacion:dd/MM/yyyy}</td></tr>
                <tr><td style='border: 1px solid #ddd; padding: 8px;'><strong>Días pendiente</strong></td><td style='border: 1px solid #ddd; padding: 8px; color: #b00020;'><strong>{item.DiasPendiente} día(s)</strong></td></tr>
            </table>
            <p>Por favor coordinar la atención a la brevedad.</p>
            <p style='font-size: 12px; color: #666;'>Este correo se generó desde el módulo de Interconsultas de Salud Ocupacional.</p>
            ";
        }

        private static string BuildBodyConsolidado(string proyectoNombre, string? razonSocial, List<InterconsultaEnvioInfoDto> items)
        {
            var filas = string.Join("", items.Select(it => $@"
                <tr>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{it.WorkerNombre}</td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{it.WorkerDni}</td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{it.Especialidad}</td>
                    <td style='border: 1px solid #ddd; padding: 8px;'>{it.FechaDerivacion:dd/MM/yyyy}</td>
                    <td style='border: 1px solid #ddd; padding: 8px; color: #b00020;'><strong>{it.DiasPendiente} d.</strong></td>
                </tr>"));

            return $@"
            <p>Estimados,</p>
            <p>
                Se listan los trabajadores de <strong>{proyectoNombre}</strong>{(string.IsNullOrWhiteSpace(razonSocial) ? "" : $" ({razonSocial})")}
                con <strong>interconsulta pendiente</strong>. Al no contar con correo propio, se solicita al
                administrador encargado del proyecto coordinar la atención de cada uno:
            </p>
            <table style='border-collapse: collapse; font-family: Arial, sans-serif; font-size: 14px; width: 100%;'>
                <thead>
                    <tr>
                        <th style='border: 1px solid #ddd; padding: 8px; background: #f3f4f6;'>Trabajador</th>
                        <th style='border: 1px solid #ddd; padding: 8px; background: #f3f4f6;'>DNI</th>
                        <th style='border: 1px solid #ddd; padding: 8px; background: #f3f4f6;'>Especialidad</th>
                        <th style='border: 1px solid #ddd; padding: 8px; background: #f3f4f6;'>Derivación</th>
                        <th style='border: 1px solid #ddd; padding: 8px; background: #f3f4f6;'>Días pendiente</th>
                    </tr>
                </thead>
                <tbody>{filas}</tbody>
            </table>
            <p style='font-size: 12px; color: #666;'>Este correo se generó desde el módulo de Interconsultas de Salud Ocupacional.</p>
            ";
        }

        public Task<InterconsultaDetalleDto> GetById(int id) => _repo.GetById(id);

        public Task<int> Create(InterconsultaCreateDto dto, int? userId)
        {
            if (dto.EmoId <= 0) throw new AbrilException("El EMO es obligatorio.", 400);
            if (dto.WorkerId <= 0) throw new AbrilException("El trabajador es obligatorio.", 400);
            if (string.IsNullOrWhiteSpace(dto.Especialidad))
                throw new AbrilException("La especialidad es obligatoria.", 400);
            return _repo.Create(dto, userId);
        }

        public Task Update(int id, InterconsultaUpdateDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Especialidad))
                throw new AbrilException("La especialidad es obligatoria.", 400);
            return _repo.Update(id, dto, userId);
        }

        public Task UpdateResultado(int id, InterconsultaResultadoPatchDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Estado) || !EstadosValidos.Contains(dto.Estado))
                throw new AbrilException("El estado de la interconsulta no es válido.", 400);
            return _repo.UpdateResultado(id, dto, userId);
        }

        public Task UpdateDerivacion(int id, InterconsultaDerivacionPatchDto dto, int? userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Especialidad))
                throw new AbrilException("La especialidad es obligatoria.", 400);
            return _repo.UpdateDerivacion(id, dto, userId);
        }
    }
}
