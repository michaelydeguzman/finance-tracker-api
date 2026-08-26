using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.RequestMagicLink;

/// <summary>Emails a single-use sign-in link, if the address has an account.</summary>
public sealed record RequestMagicLinkCommand(EmailOnlyRequestDto Dto) : IRequest;
