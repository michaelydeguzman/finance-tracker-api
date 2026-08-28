using FinanceTracker.Application.Services.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.RequestEmailVerification;

public sealed class RequestEmailVerificationCommandHandler : IRequestHandler<RequestEmailVerificationCommand>
{
    private readonly IAuthService _authService;

    public RequestEmailVerificationCommandHandler(IAuthService authService) => _authService = authService;

    public Task Handle(RequestEmailVerificationCommand request, CancellationToken cancellationToken) =>
        _authService.RequestEmailVerificationAsync(request.Dto, cancellationToken);
}
