using System.Text.Json.Serialization;

namespace CardTransactionApi.Dtos;

public class ErrorResponse
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    public ErrorResponse(string errorCode, string error)
    {
        ErrorCode = errorCode;
        Error = error;
    }
}
