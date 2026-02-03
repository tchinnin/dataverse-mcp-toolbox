namespace DataverseMCPToolBox.Models;

/// <summary>
/// Result of a validation operation
/// Encapsulates validation success/failure with optional error message
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Indicates whether validation succeeded
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Error message if validation failed, null if successful
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Create a successful validation result
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true, Error = null };

    /// <summary>
    /// Create a failed validation result with error message
    /// </summary>
    /// <param name="error">Error message describing validation failure</param>
    public static ValidationResult Failure(string error) => new() { IsValid = false, Error = error };

    /// <summary>
    /// Deconstruct for pattern matching and tuple assignment
    /// </summary>
    public void Deconstruct(out bool isValid, out string? error)
    {
        isValid = IsValid;
        error = Error;
    }
}
