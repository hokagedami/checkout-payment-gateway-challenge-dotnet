using Moq;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Api.Data;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using PaymentGateway.Api.Models.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentGateway.Api.Tests.Controllers;

/// <summary>
/// Base class for PaymentsController integration tests with shared setup
/// </summary>
public abstract class PaymentsControllerTestBase
{
    protected Random _random = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _random = new Random();
        _factory = new WebApplicationFactory<Program>();
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }

    /// <summary>
    /// Creates an in-memory DbContext for testing
    /// </summary>
    private static PaymentGatewayDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PaymentGatewayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PaymentGatewayDbContext(options);
    }

    /// <summary>
    /// Creates a test HTTP client with optional mock dependencies
    /// </summary>
    protected (HttpClient client, PaymentGatewayDbContext context) CreateTestClient(
        Mock<IBankClient>? mockBankClient = null)
    {
        // Create a shared in-memory context
        var context = CreateInMemoryContext();

        // Create a default mock if none provided
        var bankClient = mockBankClient ?? new Mock<IBankClient>();

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all DbContext-related registrations
                var dbContextOptionsDescriptor = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<PaymentGatewayDbContext>));
                if (dbContextOptionsDescriptor != null)
                    services.Remove(dbContextOptionsDescriptor);

                var dbContextServiceDescriptor = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(PaymentGatewayDbContext));
                if (dbContextServiceDescriptor != null)
                    services.Remove(dbContextServiceDescriptor);

                // Remove existing repository registration
                var repositoryDescriptor = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(IPaymentsRepository));
                if (repositoryDescriptor != null)
                    services.Remove(repositoryDescriptor);

                var bankClientDescriptor = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(IBankClient));
                if (bankClientDescriptor != null)
                    services.Remove(bankClientDescriptor);

                // Add the shared in-memory database context as singleton
                // This ensures the same instance is used throughout the test
                services.AddSingleton(context);

                // Add repository that uses the shared context
                services.AddScoped<IPaymentsRepository>(sp => new PaymentsRepository(context));
                services.AddSingleton(bankClient.Object);
            });
        })
        .CreateClient();

        // Add API key header for authentication
        client.DefaultRequestHeaders.Add("X-API-Key", "test-api-key-1");

        return (client, context);
    }

    /// <summary>
    /// Creates a valid payment request with default values
    /// </summary>
    protected static PostPaymentRequest CreateValidPaymentRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 12,
        ExpiryYear = 2026,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };
}
