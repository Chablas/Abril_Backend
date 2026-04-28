using Abril_Backend.Features.Habilitacion.Application.Dtos.Equipos;
using FluentValidation;

namespace Abril_Backend.Features.Habilitacion.Application.Validators
{
    public class EquipoCreateValidator : AbstractValidator<EquipoCreateDto>
    {
        public EquipoCreateValidator()
        {
            RuleFor(x => x.Tipo).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ProyectoId).GreaterThan(0);

            RuleFor(x => x.EmailAdmin).EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.EmailAdmin));
        }
    }
}
