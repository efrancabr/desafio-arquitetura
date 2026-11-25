using FluxoCaixa.Domain.Enums;
using FluentValidation;

namespace FluxoCaixa.Application.Lancamentos;

public sealed class RegistrarLancamentoCommandValidator : AbstractValidator<RegistrarLancamentoCommand>
{
    public RegistrarLancamentoCommandValidator()
    {
        RuleFor(x => x.ComercianteId).NotEmpty();
        RuleFor(x => x.Data).NotEmpty()
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("Data não pode estar no futuro.");
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Valor).GreaterThan(0).LessThanOrEqualTo(1_000_000_000m);
        RuleFor(x => x.Descricao).MaximumLength(200);
        RuleFor(x => x.IdempotencyKey).NotEmpty().Length(8, 128);
    }
}
