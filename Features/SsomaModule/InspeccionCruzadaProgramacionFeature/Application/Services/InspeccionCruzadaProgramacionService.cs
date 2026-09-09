using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Services
{
    /// <summary>
    /// Rotación circular de "Inspecciones Cruzadas" SSOMA: los proyectos de un mismo anillo se
    /// inspeccionan entre ellos, un mes a la vez, sin repetir pareja hasta agotar el anillo
    /// completo. El orden dentro del anillo se define una sola vez (a mano); cada mes el punto
    /// de partida circular ("offset") avanza uno solo, así:
    ///   mes 1: A→B, B→C, C→D, D→A   (offset = 1)
    ///   mes 2: A→C, B→D, C→A, D→B   (offset = 2)
    ///   mes 3: A→D, B→A, C→B, D→C   (offset = 3, el último posible con 4 miembros)
    ///   mes 4: vuelve a offset = 1
    /// Un anillo de 2 proyectos solo tiene un offset válido (1) — inevitablemente siempre se
    /// inspeccionan entre ellos dos.
    /// </summary>
    public class InspeccionCruzadaProgramacionService : IInspeccionCruzadaProgramacionService
    {
        private readonly IInspeccionCruzadaProgramacionRepository _repo;

        public InspeccionCruzadaProgramacionService(IInspeccionCruzadaProgramacionRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<ProyectoSimpleInspeccionCruzadaDto>> GetProyectosDisponiblesAsync()
        {
            var activos = await _repo.GetProyectosActivosAsync();
            return activos
                .Select(p => new ProyectoSimpleInspeccionCruzadaDto { ProyectoId = p.ProyectoId, Nombre = p.Nombre })
                .ToList();
        }

        // ── Anillos ──────────────────────────────────────────────────────

        public async Task<List<AnilloDto>> GetAnillosAsync()
        {
            var anillos = await _repo.GetAnillosAsync();
            var miembros = await _repo.GetTodosLosMiembrosAsync();

            return anillos.Select(a => new AnilloDto
            {
                Id = a.Id,
                Nombre = a.Nombre,
                Miembros = miembros
                    .Where(m => m.AnilloId == a.Id)
                    .OrderBy(m => m.Orden)
                    .Select(m => new MiembroAnilloDto
                    {
                        Id = m.Id,
                        ProyectoId = m.ProyectoId,
                        ProyectoNombre = m.Proyecto?.ProjectDescription ?? $"Proyecto {m.ProyectoId}",
                        Orden = m.Orden,
                        Activo = m.Activo,
                    })
                    .ToList(),
            }).ToList();
        }

        public async Task<AnilloDto> CrearAnilloAsync(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new AbrilException("El nombre del anillo es obligatorio.", 400);

            var entity = await _repo.CrearAnilloAsync(nombre.Trim());
            return new AnilloDto { Id = entity.Id, Nombre = entity.Nombre, Miembros = new() };
        }

        public async Task<MiembroAnilloDto> AgregarMiembroAsync(int anilloId, int proyectoId)
        {
            var existentes = await _repo.GetMiembrosAsync(anilloId);
            if (existentes.Any(m => m.ProyectoId == proyectoId))
                throw new AbrilException("Este proyecto ya está en el anillo.", 400);

            var todos = await _repo.GetTodosLosMiembrosAsync();
            if (todos.Any(m => m.AnilloId != anilloId && m.ProyectoId == proyectoId))
                throw new AbrilException("Este proyecto ya pertenece a otro anillo.", 400);

            var entity = await _repo.AgregarMiembroAsync(anilloId, proyectoId);
            var nombre = (await _repo.GetProyectoNombresAsync(new[] { proyectoId }))
                .GetValueOrDefault(proyectoId, $"Proyecto {proyectoId}");

            return new MiembroAnilloDto
            {
                Id = entity.Id,
                ProyectoId = entity.ProyectoId,
                ProyectoNombre = nombre,
                Orden = entity.Orden,
                Activo = entity.Activo,
            };
        }

        public Task ReordenarAsync(ReordenarDto dto)
            => _repo.ReordenarAsync(dto.Items.Select(i => (i.Id, i.Orden)).ToList());

        public Task SetActivoAsync(int id, bool activo)
            => _repo.SetActivoAsync(id, activo);

        // ── Programación ──────────────────────────────────────────────────

        public async Task<List<ProgramacionInspeccionCruzadaDto>> GetProgramacionAsync(
            int anioDesde, int mesDesde, int anioHasta, int mesHasta)
        {
            await GenerarProgramacionAsync(anioHasta, mesHasta);

            var items = await _repo.GetProgramacionAsync(anioDesde, mesDesde, anioHasta, mesHasta);
            var anillos = await _repo.GetAnillosAsync();
            var nombresAnillo = anillos.ToDictionary(a => a.Id, a => a.Nombre);

            var proyectoIds = items.SelectMany(i => new[] { i.ProyectoInspectorId, i.ProyectoInspeccionadoId });
            var nombresProyecto = await _repo.GetProyectoNombresAsync(proyectoIds);

            return items.Select(i => new ProgramacionInspeccionCruzadaDto
            {
                Id = i.Id,
                Anio = i.Anio,
                Mes = i.Mes,
                AnilloId = i.AnilloId,
                AnilloNombre = nombresAnillo.TryGetValue(i.AnilloId, out var an) ? an : $"Anillo {i.AnilloId}",
                ProyectoInspectorId = i.ProyectoInspectorId,
                ProyectoInspectorNombre = nombresProyecto.TryGetValue(i.ProyectoInspectorId, out var n1) ? n1 : $"Proyecto {i.ProyectoInspectorId}",
                ProyectoInspeccionadoId = i.ProyectoInspeccionadoId,
                ProyectoInspeccionadoNombre = nombresProyecto.TryGetValue(i.ProyectoInspeccionadoId, out var n2) ? n2 : $"Proyecto {i.ProyectoInspeccionadoId}",
                EsManual = i.EsManual,
                MotivoCambio = i.MotivoCambio,
            }).ToList();
        }

        /// <summary>
        /// Genera (si hacen falta) los meses de programación entre el último punto generado de
        /// cada anillo y <paramref name="anioHasta"/>/<paramref name="mesHasta"/>. No toca meses
        /// ya generados ni los que fueron reasignados a mano.
        /// </summary>
        private async Task GenerarProgramacionAsync(int anioHasta, int mesHasta)
        {
            var anillos = await _repo.GetAnillosAsync();
            foreach (var anillo in anillos)
            {
                var miembros = (await _repo.GetMiembrosAsync(anillo.Id))
                    .Where(m => m.Activo).OrderBy(m => m.Orden).ToList();
                if (miembros.Count < 2) continue;

                var cursor = await _repo.GetOrCreateCursorAsync(anillo.Id);
                var yaGeneroAlgunMes = cursor.UltimoAnio.HasValue && cursor.UltimoMes.HasValue;
                var (anioSiguiente, mesSiguiente) = SiguienteMes(cursor.UltimoAnio, cursor.UltimoMes);

                var n = miembros.Count;
                // Si ya se generó un mes antes, el offset guardado ya se usó — el próximo mes
                // avanza uno. Si es la primera vez que se genera este anillo, se usa tal cual
                // (por defecto 1, o el que se haya sembrado a mano al crear el anillo).
                var offset = yaGeneroAlgunMes ? SiguienteOffset(cursor.Offset, n) : NormalizarOffset(cursor.Offset, n);

                while (ClaveMes(anioSiguiente, mesSiguiente) <= ClaveMes(anioHasta, mesHasta))
                {
                    // Idempotencia: si dos llamadas concurrentes (ej. navegar rápido entre
                    // meses) intentan generar el mismo mes, la segunda se salta la inserción en
                    // vez de crear un desplazamiento distinto duplicado.
                    var yaExiste = (await _repo.GetProgramacionAsync(anioSiguiente, mesSiguiente, anioSiguiente, mesSiguiente))
                        .Any(p => p.AnilloId == anillo.Id);

                    if (!yaExiste)
                    {
                        for (var i = 0; i < n; i++)
                        {
                            var inspector = miembros[i];
                            var inspeccionado = miembros[(i + offset) % n];
                            await _repo.CrearProgramacionAsync(
                                anioSiguiente, mesSiguiente, anillo.Id, inspector.ProyectoId, inspeccionado.ProyectoId);
                        }

                        await _repo.GuardarCursorAsync(anillo.Id, offset, anioSiguiente, mesSiguiente);
                    }

                    (anioSiguiente, mesSiguiente) = SiguienteMes(anioSiguiente, mesSiguiente);
                    offset = SiguienteOffset(offset, n);
                }
            }
        }

        private static (int Anio, int Mes) SiguienteMes(int? anio, int? mes)
        {
            if (!anio.HasValue || !mes.HasValue)
            {
                var hoy = DateTime.UtcNow.AddHours(-5);
                return (hoy.Year, hoy.Month);
            }

            return mes.Value == 12 ? (anio.Value + 1, 1) : (anio.Value, mes.Value + 1);
        }

        private static int ClaveMes(int anio, int mes) => anio * 100 + mes;

        /// <summary>Offsets válidos van de 1 a n-1 (0 sería "cada quien se inspecciona a sí
        /// mismo"). Un anillo de 2 solo tiene el offset 1.</summary>
        private static int NormalizarOffset(int offset, int n)
        {
            var normalizado = ((offset - 1) % (n - 1) + (n - 1)) % (n - 1) + 1;
            return normalizado;
        }

        private static int SiguienteOffset(int offsetActual, int n) => NormalizarOffset(offsetActual + 1, n);

        // ── Edición manual ────────────────────────────────────────────────

        public async Task ReasignarAsync(int id, ReasignarProgramacionDto dto)
        {
            var programacion = await _repo.GetProgramacionByIdAsync(id)
                ?? throw new AbrilException("Programación de inspección cruzada no encontrada.", 404);

            if (dto.ProyectoInspectorId == dto.ProyectoInspeccionadoId)
                throw new AbrilException("El proyecto inspector y el inspeccionado no pueden ser el mismo.", 400);

            programacion.ProyectoInspectorId = dto.ProyectoInspectorId;
            programacion.ProyectoInspeccionadoId = dto.ProyectoInspeccionadoId;
            programacion.EsManual = true;
            programacion.MotivoCambio = dto.Motivo;
            await _repo.GuardarProgramacionAsync(programacion);
        }
    }
}
