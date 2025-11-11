using Microsoft.AspNetCore.Authentication;

namespace PaymentGateway.Api.Authentication;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
}
