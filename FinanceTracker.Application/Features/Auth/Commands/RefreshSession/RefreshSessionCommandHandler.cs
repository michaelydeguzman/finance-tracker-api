using FinanceTracker.Application.Services.Auth;
using MediatR;
using FinanceTracker.Application.Dtos.Auth;
namespace FinanceTracker.Application.Features.Auth.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandler : IRequestHandler<RefreshSessionCommand, AuthResultDto?>
{
    private readonly IAuthService _authService;

    public RefreshSessionCommandHandler(IAuthService authService) => _authService = authService;

    public Task<AuthResultDto?> Handle(RefreshSessionCommand request, CancellationToken cancellationToken) =>
        _authService.RefreshAsync(request.Dto, cancellationToken);
}
