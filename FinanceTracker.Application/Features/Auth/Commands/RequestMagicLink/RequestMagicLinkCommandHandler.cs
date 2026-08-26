using FinanceTracker.Application.Services.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.RequestMagicLink;

public sealed class RequestMagicLinkCommandHandler : IRequestHandler<RequestMagicLinkCommand>
{
    private readonly IAuthService _authService;

    public RequestMagicLinkCommandHandler(IAuthService authService) => _authService = authService;

    public Task Handle(RequestMagicLinkCommand request, CancellationToken cancellationToken) =>
        _authService.RequestMagicLinkAsync(request.Dto, cancellationToken);
}
