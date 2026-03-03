# API Response DTO Rule

## Rule
All API endpoints must return `ApiResponseDto<T>` where `T` is a response DTO.

- Do not return domain entities directly from controllers.
- Do not expose persistence models in API responses.
- Use feature/query handlers to map entities into response DTOs.

## Required Pattern

1. Define response DTO under `FinanceTracker.Application/Dtos/Responses`.
2. Add mapping (`FromEntity`) in the response DTO.
3. Controller returns `ActionResult<ApiResponseDto<T>>`.
4. Success responses: `ApiResponseDto<T>.Ok(data)`.
5. Error responses: `ApiResponseDto<T>.Fail(message)`.

## Example

- `Category` entity -> `CategoryResponseDto`
- `Frequency` entity -> `FrequencyResponseDto`

This is the standard for all current and future APIs.
