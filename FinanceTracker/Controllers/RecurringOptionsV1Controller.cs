using Asp.Versioning;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Frequencies.Queries.GetRecurringOptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Controllers
{
    [ApiVersion("1.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/recurring-options")]
    public class RecurringOptionsV1Controller : ControllerBase
    {
        private readonly ISender _sender;

        public RecurringOptionsV1Controller(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponseDto<List<FrequencyResponseDto>>>> GetRecurringOptions()
        {
            var options = await _sender.Send(new GetRecurringOptionsQuery());
            return Ok(ApiResponseDto<List<FrequencyResponseDto>>.Ok(options));
        }
    }
}
