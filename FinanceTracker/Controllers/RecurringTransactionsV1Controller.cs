using Asp.Versioning;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.RecurringTransactions;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.CancelRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;
using FinanceTracker.Application.Features.RecurringTransactions.Queries.GetRecurringTransactions;
using FinanceTracker.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.API.Controllers;

/// <summary>
/// The templates the worker expands. Everything here is scoped to the bearer token's user by
/// the model-level query filter; nothing takes an owner from the request.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/recurring-transactions")]
public class RecurringTransactionsV1Controller : ControllerBase
{
    private readonly ISender _sender;

    public RecurringTransactionsV1Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<RecurringTransactionResponseDto>>>> GetRecurringTransactions(
        [FromQuery] RecurringTransactionStatus? status,
        CancellationToken cancellationToken = default)
    {
        var templates = await _sender.Send(new GetRecurringTransactionsQuery(status), cancellationToken);
        return Ok(ApiResponseDto<List<RecurringTransactionResponseDto>>.Ok(templates));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponseDto<RecurringTransactionResponseDto>>> GetRecurringTransactionById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var template = await _sender.Send(new GetRecurringTransactionByIdQuery(id), cancellationToken);
        if (template is null)
            return NotFound(ApiResponseDto<RecurringTransactionResponseDto>.Fail("Recurring transaction not found."));

        return Ok(ApiResponseDto<RecurringTransactionResponseDto>.Ok(template));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<RecurringTransactionResponseDto>>> AddRecurringTransaction(
        [FromBody] CreateRecurringTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new CreateRecurringTransactionCommand(dto), cancellationToken);

        if (result.Outcome is not RecurringTransactionOutcome.Success)
            return Failure(result);

        return CreatedAtAction(
            nameof(GetRecurringTransactionById),
            new { id = result.Data!.Id },
            ApiResponseDto<RecurringTransactionResponseDto>.Ok(result.Data));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponseDto<RecurringTransactionResponseDto>>> UpdateRecurringTransaction(
        Guid id,
        [FromBody] UpdateRecurringTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        return Respond(await _sender.Send(new UpdateRecurringTransactionCommand(id, dto), cancellationToken));
    }

    // Transitions are POSTs to named sub-resources rather than a PATCH of a status field, so
    // that "stop generating" can never be an accidental side effect of an ordinary edit, and
    // so each transition can enforce its own rules.
    [HttpPost("{id}/pause")]
    public async Task<ActionResult<ApiResponseDto<RecurringTransactionResponseDto>>> PauseRecurringTransaction(
        Guid id,
        CancellationToken cancellationToken = default)
        => Respond(await _sender.Send(new PauseRecurringTransactionCommand(id), cancellationToken));

    [HttpPost("{id}/resume")]
    public async Task<ActionResult<ApiResponseDto<RecurringTransactionResponseDto>>> ResumeRecurringTransaction(
        Guid id,
        CancellationToken cancellationToken = default)
        => Respond(await _sender.Send(new ResumeRecurringTransactionCommand(id), cancellationToken));

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<ApiResponseDto<RecurringTransactionResponseDto>>> CancelRecurringTransaction(
        Guid id,
        CancellationToken cancellationToken = default)
        => Respond(await _sender.Send(new CancelRecurringTransactionCommand(id), cancellationToken));

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponseDto<string>>> DeleteRecurringTransaction(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new DeleteRecurringTransactionCommand(id), cancellationToken);

        if (result.Outcome is not RecurringTransactionOutcome.Success)
        {
            return StatusCode(
                StatusCodeFor(result.Outcome),
                ApiResponseDto<string>.Fail(result.Message ?? "Recurring transaction not found."));
        }

        return Ok(ApiResponseDto<string>.Ok(result.Message ?? "Recurring transaction deleted successfully."));
    }

    private ActionResult<ApiResponseDto<RecurringTransactionResponseDto>> Respond(RecurringTransactionCommandResult result)
        => result.Outcome is RecurringTransactionOutcome.Success
            ? Ok(ApiResponseDto<RecurringTransactionResponseDto>.Ok(result.Data!))
            : Failure(result);

    private ActionResult<ApiResponseDto<RecurringTransactionResponseDto>> Failure(RecurringTransactionCommandResult result)
        => StatusCode(
            StatusCodeFor(result.Outcome),
            ApiResponseDto<RecurringTransactionResponseDto>.Fail(result.Message ?? "Request could not be completed."));

    private static int StatusCodeFor(RecurringTransactionOutcome outcome) => outcome switch
    {
        RecurringTransactionOutcome.NotFound => StatusCodes.Status404NotFound,
        RecurringTransactionOutcome.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
