using FinanceTracker.Application.Services.Auth;
using MediatR;
using FinanceTracker.Application.Dtos.Auth;
namespace FinanceTracker.Application.Features.Auth.Commands.ExchangeExternalLogin;

public sealed class ExchangeExternalLoginCommandHandler : IRequestHandler<ExchangeExternalLoginCommand, AuthResultDto>
{
    private readonly IAuthService _authService;

    public ExchangeExternalLoginCommandHandler(IAuthService authService) => _authService = authService;

    public Task<AuthResultDto> Handle(ExchangeExternalLoginCommand request, CancellationToken cancellationToken) =>
        _authService.ExchangeExternalLoginAsync(request.Dto, cancellationToken);
}
