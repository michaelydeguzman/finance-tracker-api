using FinanceTracker.Application.Services.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand>
{
    private readonly IAuthService _authService;

    public RequestPasswordResetCommandHandler(IAuthService authService) => _authService = authService;

    public Task Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken) =>
        _authService.RequestPasswordResetAsync(request.Dto, cancellationToken);
}
