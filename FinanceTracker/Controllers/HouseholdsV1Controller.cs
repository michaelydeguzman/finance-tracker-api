using Asp.Versioning;
using FinanceTracker.Application.Dtos.Households;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Households;
using FinanceTracker.Application.Features.Households.Commands.CreateHousehold;
using FinanceTracker.Application.Features.Households.Commands.InviteHouseholdMember;
using FinanceTracker.Application.Features.Households.Commands.LeaveHousehold;
using FinanceTracker.Application.Features.Households.Commands.RemoveHouseholdMember;
using FinanceTracker.Application.Features.Households.Commands.RenameHousehold;
using FinanceTracker.Application.Features.Households.Commands.RespondToHouseholdInvitation;
using FinanceTracker.Application.Features.Households.Commands.RevokeHouseholdInvitation;
using FinanceTracker.Application.Features.Households.Queries.GetMyHousehold;
using FinanceTracker.Application.Features.Households.Queries.GetMyInvitations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.API.Controllers
{
    /// <summary>
    /// Membership, not money. Nothing here reads or writes a financial record — it changes
    /// who the tenancy filter lets read them, which is why every mutation is either the
    /// owner's to make or the invitee's own answer about their own data.
    /// </summary>
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    [Route("api/v{version:apiVersion}/households")]
    public class HouseholdsV1Controller : ControllerBase
    {
        private readonly ISender _sender;

        public HouseholdsV1Controller(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>The caller's household, or a successful response carrying null.</summary>
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponseDto<HouseholdResponseDto>>> GetMyHousehold()
        {
            var household = await _sender.Send(new GetMyHouseholdQuery());

            // Null is an answer, not a miss: "you are not in a household" is the normal state
            // for most accounts, and a 404 would have every client treating it as an error.
            return Ok(ApiResponseDto<HouseholdResponseDto>.Ok(household!));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponseDto<HouseholdResponseDto>>> CreateHousehold(
            [FromBody] CreateHouseholdDto dto)
        {
            var result = await _sender.Send(new CreateHouseholdCommand(dto));

            return result.Outcome == HouseholdOutcome.Success
                ? CreatedAtAction(nameof(GetMyHousehold), null, ApiResponseDto<HouseholdResponseDto>.Ok(result.Data!))
                : Failure(result);
        }

        [HttpPut("me")]
        public async Task<ActionResult<ApiResponseDto<HouseholdResponseDto>>> RenameHousehold(
            [FromBody] RenameHouseholdDto dto)
        {
            var result = await _sender.Send(new RenameHouseholdCommand(dto));

            return result.Outcome == HouseholdOutcome.Success
                ? Ok(ApiResponseDto<HouseholdResponseDto>.Ok(result.Data!))
                : Failure(result);
        }

        [HttpPost("me/leave")]
        public async Task<ActionResult<ApiResponseDto<object>>> LeaveHousehold()
        {
            var result = await _sender.Send(new LeaveHouseholdCommand());

            return result.Outcome == HouseholdOutcome.Success
                ? Ok(ApiResponseDto<object>.Ok(null!, result.Message ?? "You have left the household."))
                : Failure(result);
        }

        [HttpDelete("me/members/{memberUserId:guid}")]
        public async Task<ActionResult<ApiResponseDto<HouseholdResponseDto>>> RemoveMember(Guid memberUserId)
        {
            var result = await _sender.Send(new RemoveHouseholdMemberCommand(memberUserId));

            return result.Outcome == HouseholdOutcome.Success
                ? Ok(ApiResponseDto<HouseholdResponseDto>.Ok(result.Data!))
                : Failure(result);
        }

        [HttpPost("me/invitations")]
        public async Task<ActionResult<ApiResponseDto<HouseholdInvitationDto>>> Invite(
            [FromBody] InviteHouseholdMemberDto dto)
        {
            var result = await _sender.Send(new InviteHouseholdMemberCommand(dto));

            return result.Outcome == HouseholdOutcome.Success
                ? Ok(ApiResponseDto<HouseholdInvitationDto>.Ok(result.Data!, "Invitation sent."))
                : Failure(result);
        }

        [HttpDelete("me/invitations/{invitationId:guid}")]
        public async Task<ActionResult<ApiResponseDto<object>>> RevokeInvitation(Guid invitationId)
        {
            var result = await _sender.Send(new RevokeHouseholdInvitationCommand(invitationId));

            return result.Outcome == HouseholdOutcome.Success
                ? Ok(ApiResponseDto<object>.Ok(null!, result.Message ?? "Invitation revoked."))
                : Failure(result);
        }

        /// <summary>Invitations addressed to the caller's own email address.</summary>
        [HttpGet("invitations")]
        public async Task<ActionResult<ApiResponseDto<List<HouseholdInvitationDto>>>> GetMyInvitations()
        {
            var invitations = await _sender.Send(new GetMyInvitationsQuery());
            return Ok(ApiResponseDto<List<HouseholdInvitationDto>>.Ok(invitations));
        }

        [HttpPost("invitations/{invitationId:guid}/accept")]
        public async Task<ActionResult<ApiResponseDto<HouseholdResponseDto>>> AcceptInvitation(Guid invitationId)
        {
            var result = await _sender.Send(new RespondToHouseholdInvitationCommand(invitationId, Accept: true));

            return result.Outcome == HouseholdOutcome.Success
                ? Ok(ApiResponseDto<HouseholdResponseDto>.Ok(result.Data!))
                : Failure(result);
        }

        [HttpPost("invitations/{invitationId:guid}/decline")]
        public async Task<ActionResult<ApiResponseDto<HouseholdResponseDto>>> DeclineInvitation(Guid invitationId)
        {
            var result = await _sender.Send(new RespondToHouseholdInvitationCommand(invitationId, Accept: false));

            return result.Outcome == HouseholdOutcome.Success
                ? Ok(ApiResponseDto<HouseholdResponseDto>.Ok(null!, result.Message ?? "Invitation declined."))
                : Failure(result);
        }

        /// <summary>
        /// The one place an outcome becomes a status code. Written once rather than per action
        /// so a new endpoint cannot map Forbidden to a 404 or Conflict to a 400 by oversight.
        /// </summary>
        private ObjectResult Failure<T>(HouseholdResult<T> result)
        {
            var body = ApiResponseDto<T>.Fail(result.Message ?? "The request could not be completed.");

            return result.Outcome switch
            {
                HouseholdOutcome.NotFound => NotFound(body),
                HouseholdOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, body),
                HouseholdOutcome.Conflict => Conflict(body),
                _ => BadRequest(body)
            };
        }
    }
}
