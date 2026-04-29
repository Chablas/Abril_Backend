using Abril_Backend.Features.Habilitacion.Application.Dtos.SctrVidaley;
using FluentValidation;

namespace Abril_Backend.Features.Habilitacion.Application.Validators
{
    public class SctrVidaLeyCreateValidator : AbstractValidator<SctrVidaLeyCreateDto>
    {
        public SctrVidaLeyCreateValidator()
        {
            RuleFor(x => x.ProyectoId).GreaterThan(0);

            RuleFor(x => x.Tipo).Must(t => t == "SCTR" || t == "VIDA_LEY")
                .WithMessage("Tipo debe ser SCTR o VIDA_LEY.");

            RuleFor(x => x.Mes).InclusiveBetween(1, 12);
            RuleFor(x => x.Anio).GreaterThan(2020);

            RuleFor(x => x.WorkerIds).NotEmpty()
                .WithMessage("Debe seleccionar al menos un trabajador.");
        }
    }
}
