namespace SatelliteTracker.Database.Common;

// This class represents the result of an operation, which can either be a success with a value or a failure with an error message.
// To be used in service methods to return either a successful result or an error without throwing exceptions.
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Value { get; private set; } // The value of the result if the operation was successful; null if it failed.
    public string? Error { get; private set; } // The error message if the operation failed; null if it succeeded.

    private Result() { }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}
