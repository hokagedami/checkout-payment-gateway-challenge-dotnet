namespace PaymentGateway.Api.Models.Responses;

/// <summary>
/// Unified API response wrapper for all endpoints
/// </summary>
/// <typeparam name="T">Type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates if the request was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The response data (null if request failed)
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// List of error messages (empty if successful)
    /// </summary>
    public List<string> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful response
    /// </summary>
    public static ApiResponse<T> SuccessResponse(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Errors = []
        };
    }

    /// <summary>
    /// Creates an error response
    /// </summary>
    public static ApiResponse<T> ErrorResponse(List<string> errors, T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = data,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates an error response with a single error message
    /// </summary>
    public static ApiResponse<T> ErrorResponse(string error, T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = data,
            Errors = [error]
        };
    }
}
