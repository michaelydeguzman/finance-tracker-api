namespace FinanceTracker.Application.Dtos.Responses;

public sealed class ApiResponseDto<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }

    public static ApiResponseDto<T> Ok(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };

    public static ApiResponseDto<T> Fail(string message)
        => new() { Success = false, Message = message };
}
