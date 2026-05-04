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

            RuleFor(x => x.TipoPoliza)
                .Must(t => t == "Renovacion" || t == "Inclusion")
                .WithMessage("TipoPoliza debe ser Renovacion o Inclusion.");

            RuleFor(x => x.Workers).NotEmpty()
                .WithMessage("Debe seleccionar al menos un trabajador.");

            RuleForEach(x => x.Workers)
                .ChildRules(w => w.RuleFor(x => x.WorkerId).GreaterThan(0));
        }
    }
}
