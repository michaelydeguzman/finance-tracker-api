using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.RequestEmailVerification;

/// <summary>Emails a fresh confirmation link, if the address has an unconfirmed account.</summary>
public sealed record RequestEmailVerificationCommand(EmailOnlyRequestDto Dto) : IRequest;
