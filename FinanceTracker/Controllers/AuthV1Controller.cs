using Asp.Versioning;
using FinanceTracker.API.Authentication;
using FinanceTracker.Application.Dtos.Auth;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Auth.Commands.ConsumeMagicLink;
using FinanceTracker.Application.Features.Auth.Commands.ExchangeExternalLogin;
using FinanceTracker.Application.Features.Auth.Commands.Login;
using FinanceTracker.Application.Features.Auth.Commands.RefreshSession;
using FinanceTracker.Application.Features.Auth.Commands.Register;
using FinanceTracker.Application.Features.Auth.Commands.RequestMagicLink;
using FinanceTracker.Application.Features.Auth.Commands.RequestPasswordReset;
using FinanceTracker.Application.Features.Auth.Commands.ResetPassword;
using FinanceTracker.Application.Features.Auth.Commands.VerifyEmail;
using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinanceTracker.API.Controllers
{
    [ApiVersion("1.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/auth")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public class AuthV1Controller : ControllerBase
    {
        /// <summary>
        /// The single answer given to every request that must not reveal whether an address
        /// has an account. Registration, magic link and password reset all return this
        /// whatever happened, so none of them can be used to enumerate users.
        /// </summary>
        private const string NeutralAcknowledgement =
            "If that email address has an account, we have sent it a message.";

        private readonly ISender _sender;
        private readonly AuthOptions _authOptions;

        public AuthV1Controller(ISender sender, IOptions<AuthOptions> authOptions)
        {
            _sender = sender;
            _authOptions = authOptions.Value;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ApiResponseDto<object>>> Register([FromBody] RegisterRequestDto dto)
        {
            if (PasswordIsTooWeak(dto.Password, out var problem))
                return BadRequest(ApiResponseDto<object>.Fail(problem));

            if (!LooksLikeAnEmail(dto.Email))
                return BadRequest(ApiResponseDto<object>.Fail("A valid email address is required."));

            await _sender.Send(new RegisterCommand(dto));
            return Accepted(ApiResponseDto<object>.Ok(null!, NeutralAcknowledgement));
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponseDto<AuthResultDto>>> Login([FromBody] LoginRequestDto dto)
        {
            var result = await _sender.Send(new LoginCommand(dto));

            // One message for every failure mode. "No such account" and "wrong password"
            // must not be distinguishable.
            return result is null
                ? Unauthorized(ApiResponseDto<AuthResultDto>.Fail("Email or password is incorrect."))
                : Ok(ApiResponseDto<AuthResultDto>.Ok(result));
        }

        [HttpPost("exchange")]
        [BffOnly]
        public async Task<ActionResult<ApiResponseDto<AuthResultDto>>> Exchange([FromBody] ExternalLoginRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ProviderSubject) || !LooksLikeAnEmail(dto.Email))
                return BadRequest(ApiResponseDto<AuthResultDto>.Fail("Provider subject and email are required."));

            try
            {
                var result = await _sender.Send(new ExchangeExternalLoginCommand(dto));
                return Ok(ApiResponseDto<AuthResultDto>.Ok(result));
            }
            catch (AccountLinkingConflictException)
            {
                return Conflict(ApiResponseDto<AuthResultDto>.Fail(
                    "That email is already registered. Sign in with your password to link this provider."));
            }
        }

        [HttpPost("magic-link/request")]
        public async Task<ActionResult<ApiResponseDto<object>>> RequestMagicLink([FromBody] EmailOnlyRequestDto dto)
        {
            await _sender.Send(new RequestMagicLinkCommand(dto));
            return Accepted(ApiResponseDto<object>.Ok(null!, NeutralAcknowledgement));
        }

        [HttpPost("magic-link/consume")]
        public async Task<ActionResult<ApiResponseDto<AuthResultDto>>> ConsumeMagicLink([FromBody] TokenRequestDto dto)
        {
            var result = await _sender.Send(new ConsumeMagicLinkCommand(dto));

            return result is null
                ? Unauthorized(ApiResponseDto<AuthResultDto>.Fail("That sign-in link is no longer valid."))
                : Ok(ApiResponseDto<AuthResultDto>.Ok(result));
        }

        [HttpPost("password-reset/request")]
        public async Task<ActionResult<ApiResponseDto<object>>> RequestPasswordReset([FromBody] EmailOnlyRequestDto dto)
        {
            await _sender.Send(new RequestPasswordResetCommand(dto));
            return Accepted(ApiResponseDto<object>.Ok(null!, NeutralAcknowledgement));
        }

        [HttpPost("password-reset/confirm")]
        public async Task<ActionResult<ApiResponseDto<object>>> ResetPassword([FromBody] ResetPasswordRequestDto dto)
        {
            if (PasswordIsTooWeak(dto.NewPassword, out var problem))
                return BadRequest(ApiResponseDto<object>.Fail(problem));

            var succeeded = await _sender.Send(new ResetPasswordCommand(dto));

            return succeeded
                ? Ok(ApiResponseDto<object>.Ok(null!, "Your password has been changed."))
                : BadRequest(ApiResponseDto<object>.Fail("That reset link is no longer valid."));
        }

        [HttpPost("verify-email")]
        public async Task<ActionResult<ApiResponseDto<object>>> VerifyEmail([FromBody] TokenRequestDto dto)
        {
            var succeeded = await _sender.Send(new VerifyEmailCommand(dto));

            return succeeded
                ? Ok(ApiResponseDto<object>.Ok(null!, "Your email address is confirmed."))
                : BadRequest(ApiResponseDto<object>.Fail("That confirmation link is no longer valid."));
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponseDto<AuthResultDto>>> Refresh([FromBody] TokenRequestDto dto)
        {
            var result = await _sender.Send(new RefreshSessionCommand(dto));

            return result is null
                ? Unauthorized(ApiResponseDto<AuthResultDto>.Fail("Your session has expired. Please sign in again."))
                : Ok(ApiResponseDto<AuthResultDto>.Ok(result));
        }

        private bool PasswordIsTooWeak(string? password, out string problem)
        {
            if (string.IsNullOrEmpty(password) || password.Length < _authOptions.MinimumPasswordLength)
            {
                problem = $"Password must be at least {_authOptions.MinimumPasswordLength} characters.";
                return true;
            }

            problem = string.Empty;
            return false;
        }

        private static bool LooksLikeAnEmail(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var at = value.IndexOf('@');
            return at > 0 && at < value.Length - 1 && value.IndexOf('@', at + 1) < 0;
        }
    }
}
