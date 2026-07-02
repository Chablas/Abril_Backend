using Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Dtos;

namespace Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Interfaces
{
    public interface ICumpleanosService
    {
        /// <summary>
        /// Devuelve los cumpleañeros del trimestre (1-4) con su foto de Graph ya resuelta.
        /// </summary>
        Task<TrimestreCumpleanosDto> GetTrimestre(int trimestre);
    }
}
