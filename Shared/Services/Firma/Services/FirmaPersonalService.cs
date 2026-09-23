using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Firma.Dtos;
using Abril_Backend.Shared.Services.Firma.Interfaces;

namespace Abril_Backend.Shared.Services.Firma.Services
{
    /// <inheritdoc cref="IFirmaPersonalService"/>
    public class FirmaPersonalService : IFirmaPersonalService
    {
        private readonly IFirmaPersonalRepository _repository;

        public FirmaPersonalService(IFirmaPersonalRepository repository)
        {
            _repository = repository;
        }

        public Task<FirmaPersonalEstadoDto> GetEstado(int userId) => _repository.GetEstadoByUserId(userId);

        public async Task<FirmaPersonalEstadoDto> Save(FirmaPersonalSaveDto dto, int userId)
        {
            // Sin tipo se asume DIBUJO: es lo único que existía antes de que la firma tuviera tipos
            // y lo único que siguen mandando Contabilidad → Firma y "Tu firma".
            var tipo = (dto?.Tipo ?? FirmaTipo.CodigoDibujo).Trim().ToUpperInvariant();

            // Mismas reglas para todos: las pantallas que registran firma escriben las mismas filas
            // de person_firma y el mismo helper estampa el PDF, así que lo que se acepta acá tiene
            // que ser estampable desde cualquiera de ellas.
            var bytes = tipo switch
            {
                FirmaTipo.CodigoDibujo => FirmaImagenHelper.DecodePng(dto?.ImageBase64),
                FirmaTipo.CodigoImagen => FirmaImagenHelper.DecodeImagenSubida(dto?.ImageBase64),
                _ => throw new AbrilException($"El tipo de firma '{tipo}' no existe.", 400),
            };

            return await _repository.Upsert(userId, tipo, bytes, FirmaImagenHelper.Mime);
        }

        public Task<List<FirmaTipoDto>> GetTipos() => _repository.GetTipos();

        public async Task<List<FirmaTipoDto>> SaveTipos(FirmaTiposSaveDto dto, int userId)
        {
            var tipos = dto?.Tipos ?? new List<FirmaTipoActivoDto>();

            if (tipos.Count == 0)
                throw new AbrilException("No se recibió ningún tipo de firma.", 400);

            foreach (var t in tipos)
                t.Codigo = (t.Codigo ?? string.Empty).Trim().ToUpperInvariant();

            var duplicado = tipos.GroupBy(t => t.Codigo).FirstOrDefault(g => g.Count() > 1);
            if (duplicado != null)
                throw new AbrilException($"El tipo de firma '{duplicado.Key}' vino repetido.", 400);

            // Apagar los dos dejaría a quien firma un consolidado sin ninguna forma de registrar su
            // firma: el modal no tendría nada que mostrar y la aprobación quedaría bloqueada para
            // todos. Se corta acá y no en la pantalla porque el endpoint también se puede llamar solo.
            var vigentes = await _repository.GetTipos();
            var quedanActivos = vigentes
                .Select(v => tipos.FirstOrDefault(t => t.Codigo == v.Codigo)?.Activo ?? v.Activo)
                .Any(activo => activo);

            if (!quedanActivos)
                throw new AbrilException(
                    "Tiene que quedar habilitado al menos un tipo de firma: si no, nadie podría registrar la suya para firmar.",
                    400);

            var desconocido = tipos.FirstOrDefault(t => vigentes.All(v => v.Codigo != t.Codigo));
            if (desconocido != null)
                throw new AbrilException($"El tipo de firma '{desconocido.Codigo}' no existe.", 400);

            return await _repository.SaveTipos(tipos, userId);
        }
    }
}
