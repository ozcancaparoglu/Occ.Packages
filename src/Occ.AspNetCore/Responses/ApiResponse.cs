namespace Occ.AspNetCore.Responses;

public sealed record ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<ApiError>? Errors { get; init; }

    public static ApiResponse<T> Success(T data) =>
        new() { IsSuccess = true, Data = data };

    public static ApiResponse<T> Failure(IReadOnlyList<ApiError> errors) =>
        new() { IsSuccess = false, Errors = errors };
}

public sealed record ApiError(string Code, string Message);