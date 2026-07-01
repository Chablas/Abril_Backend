using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Dtos;
using Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Interfaces;
using Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Graph.Interfaces;

namespace Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Services
{
    public class CumpleanosService : ICumpleanosService
    {
        private readonly ICumpleanosRepository _repo;
        private readonly IUserPhotoService _photoService;

        public CumpleanosService(ICumpleanosRepository repo, IUserPhotoService photoService)
        {
            _repo = repo;
            _photoService = photoService;
        }

        public async Task<TrimestreCumpleanosDto> GetTrimestre(int trimestre)
        {
            if (trimestre < 1 || trimestre > 4)
                throw new AbrilException("El trimestre debe estar entre 1 y 4.", 400);

            var cumpleaneros = await _repo.GetCumpleaneros(trimestre);

            if (cumpleaneros.Count > 0)
            {
                var emails = cumpleaneros
                    .Select(c => c.Email)
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var fotos = await _photoService.GetPhotosByEmailsAsync(emails);

                foreach (var c in cumpleaneros)
                {
                    if (!string.IsNullOrWhiteSpace(c.Email) &&
                        fotos.TryGetValue(c.Email, out var foto))
                    {
                        c.FotoBase64 = foto;
                    }
                }
            }

            return new TrimestreCumpleanosDto
            {
                Trimestre = trimestre,
                Cumpleaneros = cumpleaneros,
            };
        }
    }
}
