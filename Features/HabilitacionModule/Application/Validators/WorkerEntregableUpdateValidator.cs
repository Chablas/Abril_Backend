using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using FluentValidation;

namespace Abril_Backend.Features.Habilitacion.Application.Validators
{
    public class WorkerEntregableUpdateValidator : AbstractValidator<WorkerEntregableUpdateDto>
    {
        private static readonly string[] EstadosValidos = { "Falta", "Enviado", "Aprobado", "Rechazado", "No Aplica", "En Plazo", "Vencido" };

        public WorkerEntregableUpdateValidator()
        {
            RuleFor(x => x.Estado).NotEmpty()
                .Must(e => EstadosValidos.Contains(e))
                .WithMessage("Estado inválido.");

            RuleFor(x => x.Vigencia).GreaterThan(DateTime.Today)
                .When(x => x.Vigencia.HasValue)
                .WithMessage("La vigencia debe ser una fecha futura.");
        }
    }
}
