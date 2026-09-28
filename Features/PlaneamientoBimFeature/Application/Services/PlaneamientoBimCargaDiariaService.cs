using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Services
{
    public class PlaneamientoBimCargaDiariaService : IPlaneamientoBimCargaDiariaService
    {
        private const int VentanaDiasEdicion = 5; // hoy y los 4 días anteriores
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB por foto
        private const int MaxArchivosPorSubida = 20;

        private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp",
        };

        private static readonly string[] CategoriasValidas = { "GENERAL", "PROCURA" };

        private readonly IPlaneamientoBimCargaDiariaRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;

        public PlaneamientoBimCargaDiariaService(
            IPlaneamientoBimCargaDiariaRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
        }

        public async Task<CargaDiariaDto> GetCargaDiaria(int projectId, DateOnly fecha, string categoria = "GENERAL")
        {
            ValidarCategoria(categoria);

            var dto = await _repository.GetCargaDiaria(projectId, fecha, categoria);
            if (dto == null)
                throw new AbrilException("El proyecto no existe.", 404);

            dto.EsEditable = EsFechaEditable(fecha);
            return dto;
        }

        public async Task GuardarCargaDiaria(int projectId, DateOnly fecha, CargaDiariaUpdateDto dto, int userId)
        {
            ValidarVentanaDeEdicion(fecha);

            if (dto.Celdas.Any(c => c.Cumplida.HasValue && c.Cumplida.Value == false && c.CausaId == null))
                throw new AbrilException("Debe indicar la causa de no cumplimiento para las celdas marcadas como no hechas.", 400);

            await ValidarSectores(projectId, dto.Celdas);

            await _repository.GuardarCargaDiaria(projectId, fecha, dto, userId);
        }

        /// <summary>SectorId es un número derivado (1..N), no una FK — se valida acá que
        /// esté dentro del rango que le corresponde al nivel según su TipoEstructura y los
        /// conteos de la torre (ver NivelRangoSectorDto). Nunca se llega a la BD con un
        /// sector fuera de rango.</summary>
        private async Task ValidarSectores(int projectId, List<CeldaUpdateDto> celdas)
        {
            var evaluadas = celdas.Where(c => c.Cumplida.HasValue).ToList();
            if (evaluadas.Count == 0)
                return;

            var rangos = (await _repository.GetRangosSectorPorNivel(projectId)).ToDictionary(r => r.NivelId);

            foreach (var celda in evaluadas)
            {
                if (!rangos.TryGetValue(celda.NivelId, out var nivel))
                    throw new AbrilException($"El nivel {celda.NivelId} no pertenece a este proyecto.", 400);

                var maxSectores = nivel.TipoEstructura switch
                {
                    "SUBESTRUCTURA" => nivel.CantidadSectoresSubestructura,
                    "SUPERESTRUCTURA" => nivel.CantidadSectoresSuperestructura,
                    _ => 0,
                };

                if (maxSectores > 0 && (celda.SectorId < 1 || celda.SectorId > maxSectores))
                    throw new AbrilException(
                        $"El sector {celda.SectorId} no es válido para el nivel {celda.NivelId} (rango permitido: 1 a {maxSectores}).", 400);
            }
        }

        public async Task<List<EvidenciaFotoDto>> SubirEvidencias(int projectId, DateOnly fecha, IFormFileCollection files, int userId, string categoria = "GENERAL")
        {
            ValidarCategoria(categoria);
            ValidarVentanaDeEdicion(fecha);

            if (files is null || files.Count == 0)
                throw new AbrilException("No se adjuntó ninguna foto.", 400);
            if (files.Count > MaxArchivosPorSubida)
                throw new AbrilException($"Solo se pueden subir hasta {MaxArchivosPorSubida} fotos por vez.", 400);

            foreach (var file in files)
            {
                if (file.Length == 0)
                    throw new AbrilException($"El archivo \"{file.FileName}\" está vacío.", 400);
                if (file.Length > MaxFileSizeBytes)
                    throw new AbrilException($"El archivo \"{file.FileName}\" supera el tamaño máximo permitido (10 MB).", 400);
                var extension = Path.GetExtension(file.FileName);
                if (!ExtensionesPermitidas.Contains(extension))
                    throw new AbrilException($"El tipo de archivo \"{extension}\" no está permitido. Use JPG, PNG o WEBP.", 400);
            }

            var container = _containerResolver.GetProjectFotosContainerName();
            var streams = new List<Stream>();
            try
            {
                var toUpload = new List<(Stream Stream, string FileName)>();
                foreach (var file in files)
                {
                    var stream = file.OpenReadStream();
                    streams.Add(stream);
                    var extension = Path.GetExtension(file.FileName);
                    toUpload.Add((stream, $"{Guid.NewGuid()}{extension}"));
                }

                var urls = await _fileStorageService.UploadFilesAsync(toUpload, container);
                return await _repository.AgregarEvidencias(projectId, fecha, urls, userId, categoria);
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        private static readonly TimeZoneInfo LimaZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

        private static DateOnly HoyLima()
            => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LimaZone));

        private static bool EsFechaEditable(DateOnly fecha)
        {
            var hoy = HoyLima();
            return fecha <= hoy && fecha >= hoy.AddDays(-(VentanaDiasEdicion - 1));
        }

        private static void ValidarVentanaDeEdicion(DateOnly fecha)
        {
            var hoy = HoyLima();
            if (fecha > hoy)
                throw new AbrilException("No se puede cargar información de una fecha futura.", 400);
            if (!EsFechaEditable(fecha))
                throw new AbrilException($"La fecha está fuera de la ventana de edición (últimos {VentanaDiasEdicion} días).", 409);
        }

        private static void ValidarCategoria(string categoria)
        {
            if (!CategoriasValidas.Contains(categoria))
                throw new AbrilException($"Categoría inválida. Use {string.Join(" o ", CategoriasValidas)}.", 400);
        }
    }
}
