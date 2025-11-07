using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Services;

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankClient> _logger;

    public BankClient(HttpClient httpClient, ILogger<BankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/payments", request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BankPaymentResponse>();
            }

            _logger.LogWarning("Bank returned non-success status code: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling bank API");
            return null;
        }
    }
}
