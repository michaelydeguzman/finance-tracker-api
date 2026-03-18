using Asp.Versioning;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.Features.Transactions.Queries.GetAllTransactions;
using FinanceTracker.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Controllers;

[ApiVersion("1.0")]
[ApiController]
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
        if (!ModelState.IsValid)
            return BadRequest(ApiResponseDto<TransactionResponseDto>.Fail("Invalid request payload."));

        var response = await _sender.Send(new CreateTransactionCommand(dto));
        return Created(string.Empty, ApiResponseDto<TransactionResponseDto>.Ok(response));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<TransactionResponseDto>>>> GetTransactions([FromQuery] CategoryType? categoryType)
    {
        var transactions = await _sender.Send(new GetAllTransactionsQuery(categoryType));
        return Ok(ApiResponseDto<List<TransactionResponseDto>>.Ok(transactions));
    }
}
