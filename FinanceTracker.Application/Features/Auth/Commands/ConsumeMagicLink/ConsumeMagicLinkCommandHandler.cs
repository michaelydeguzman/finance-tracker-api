using FinanceTracker.Application.Services.Auth;
using MediatR;
using FinanceTracker.Application.Dtos.Auth;
namespace FinanceTracker.Application.Features.Auth.Commands.ConsumeMagicLink;

public sealed class ConsumeMagicLinkCommandHandler : IRequestHandler<ConsumeMagicLinkCommand, AuthResultDto?>
{
    private readonly IAuthService _authService;

    public ConsumeMagicLinkCommandHandler(IAuthService authService) => _authService = authService;

    public Task<AuthResultDto?> Handle(ConsumeMagicLinkCommand request, CancellationToken cancellationToken) =>
        _authService.ConsumeMagicLinkAsync(request.Dto, cancellationToken);
}
