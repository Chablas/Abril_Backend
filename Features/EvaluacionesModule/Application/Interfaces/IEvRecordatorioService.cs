namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvRecordatorioService
    {
        Task<object> ProcesarRecordatoriosAsync();
        Task<object> ProcesarDescargoAsync();
    }
}
