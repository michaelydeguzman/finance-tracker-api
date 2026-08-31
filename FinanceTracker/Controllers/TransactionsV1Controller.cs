using Asp.Versioning;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.Features.Transactions.Commands.DeleteTransaction;
using FinanceTracker.Application.Features.Transactions.Commands.UpdateTransaction;
using FinanceTracker.Application.Features.Transactions.Queries.GetTransactionsList;
using FinanceTracker.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/transactions")]
public class TransactionsV1Controller : ControllerBase
{
    private readonly ISender _sender;

    public TransactionsV1Controller(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<TransactionResponseDto>>> AddTransaction([FromBody] CreateTransactionDto dto)
    {
        var response = await _sender.Send(new CreateTransactionCommand(dto));
        return Created(string.Empty, ApiResponseDto<TransactionResponseDto>.Ok(response));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponseDto<TransactionResponseDto>>> UpdateTransaction(Guid id, [FromBody] UpdateTransactionDto dto)
    {
        var updated = await _sender.Send(new UpdateTransactionCommand(id, dto));
        if (updated is null)
            return NotFound(ApiResponseDto<TransactionResponseDto>.Fail("Transaction not found."));

        return Ok(ApiResponseDto<TransactionResponseDto>.Ok(updated));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponseDto<string>>> DeleteTransaction(Guid id)
    {
        var deleted = await _sender.Send(new DeleteTransactionCommand(id));
        if (!deleted)
            return NotFound(ApiResponseDto<string>.Fail("Transaction not found."));

        return Ok(ApiResponseDto<string>.Ok("Transaction deleted successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] CategoryType? categoryType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] List<Guid>? categoryIds,
        [FromQuery] int? page,
        [FromQuery] int? pageSize)
    {
        var categoryIdsKeyPresent = Request.Query.Keys.Any(k =>
            string.Equals(k, "categoryIds", StringComparison.OrdinalIgnoreCase));

        if (categoryIdsKeyPresent && (categoryIds is null || categoryIds.Count == 0))
            return BadRequest(ApiResponseDto<List<TransactionResponseDto>>.Fail(
                "categoryIds was provided but is empty. Omit the parameter or supply at least one category ID."));

        var pageSupplied = page.HasValue;
        var pageSizeSupplied = pageSize.HasValue;
        if (pageSupplied != pageSizeSupplied)
            return BadRequest(ApiResponseDto<List<TransactionResponseDto>>.Fail(
                "Both page and pageSize are required for pagination."));

        if (pageSupplied && pageSizeSupplied)
        {
            if (page is < 1)
                return BadRequest(ApiResponseDto<List<TransactionResponseDto>>.Fail("page must be >= 1."));
            if (pageSize is < 1)
                return BadRequest(ApiResponseDto<List<TransactionResponseDto>>.Fail("pageSize must be >= 1."));
            if (pageSize > 20)
                return BadRequest(ApiResponseDto<List<TransactionResponseDto>>.Fail("pageSize cannot exceed 20."));
        }

        var result = await _sender.Send(new GetTransactionsListQuery(
            categoryType,
            from,
            to,
            categoryIds,
            categoryIdsKeyPresent,
            page,
            pageSize));

        if (!result.IsPaged)
            return Ok(ApiResponseDto<List<TransactionResponseDto>>.Ok(result.Items.ToList()));

        return Ok(ApiResponseDto<PagedTransactionsResponseDto>.Ok(
            new PagedTransactionsResponseDto(result.Items.ToList(), result.TotalCount!.Value)));
    }
}
