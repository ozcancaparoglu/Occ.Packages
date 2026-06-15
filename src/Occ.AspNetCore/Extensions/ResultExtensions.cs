using Microsoft.AspNetCore.Http;
using Occ.AspNetCore.Responses;
using Occ.SharedKernal;

namespace Occ.AspNetCore.Extensions;

public static class ResultExtensions
{
    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Result, TOut> onFailure) =>
        result.IsSuccess ? onSuccess() : onFailure(result);

    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Result<TIn>, TOut> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result);

    public static IResult ToApiResponse<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Microsoft.AspNetCore.Http.Results.Ok(ApiResponse<T>.Success(result.Value));

        return Microsoft.AspNetCore.Http.Results.Json(
            ApiResponse<T>.Failure(GetErrors(result.Error)),
            statusCode: GetStatusCode(result.Error));
    }

    public static IResult ToCreatedApiResponse<T>(this Result<T> result, Func<T, string> locationFactory)
    {
        if (result.IsSuccess)
            return Microsoft.AspNetCore.Http.Results.Created(locationFactory(result.Value), ApiResponse<T>.Success(result.Value));

        return Microsoft.AspNetCore.Http.Results.Json(
            ApiResponse<T>.Failure(GetErrors(result.Error)),
            statusCode: GetStatusCode(result.Error));
    }

    private static IReadOnlyList<ApiError> GetErrors(Error error) =>
        error is ValidationError ve
            ? ve.Errors.Select(e => new ApiError(e.Code, e.Description)).ToList()
            : [new ApiError(error.Code, error.Description)];

    private static int GetStatusCode(Error error) => error.Type switch
    {
        ErrorType.NotFound   => StatusCodes.Status404NotFound,
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Conflict   => StatusCodes.Status409Conflict,
        _                    => StatusCodes.Status500InternalServerError
    };
}