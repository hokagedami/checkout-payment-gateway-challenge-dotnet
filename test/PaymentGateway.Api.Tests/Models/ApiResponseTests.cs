using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.Models;

/// <summary>
/// Tests for ApiResponse wrapper to ensure consistent response structure
/// </summary>
[TestFixture]
public class ApiResponseTests
{
    [Test]
    public void SuccessResponse_WithData_SetsCorrectProperties()
    {
        // Arrange
        var data = new { Id = 1, Name = "Test" };

        // Act
        var response = ApiResponse<object>.SuccessResponse(data);

        // Assert
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.EqualTo(data));
        Assert.That(response.Errors, Is.Empty);
    }

    [Test]
    public void SuccessResponse_WithNull_AllowsNullData()
    {
        // Act
        var response = ApiResponse<object?>.SuccessResponse(null);

        // Assert
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Null);
        Assert.That(response.Errors, Is.Empty);
    }

    [Test]
    public void ErrorResponse_WithMultipleErrors_SetsCorrectProperties()
    {
        // Arrange
        var errors = new List<string> { "Error 1", "Error 2", "Error 3" };

        // Act
        var response = ApiResponse<object>.ErrorResponse(errors);

        // Assert
        Assert.That(response.Success, Is.False);
        Assert.That(response.Data, Is.Null);
        Assert.That(response.Errors, Is.EqualTo(errors));
        Assert.That(response.Errors.Count, Is.EqualTo(3));
    }

    [Test]
    public void ErrorResponse_WithSingleError_SetsCorrectProperties()
    {
        // Arrange
        var error = "Single error message";

        // Act
        var response = ApiResponse<object>.ErrorResponse(error);

        // Assert
        Assert.That(response.Success, Is.False);
        Assert.That(response.Data, Is.Null);
        Assert.That(response.Errors, Has.Count.EqualTo(1));
        Assert.That(response.Errors[0], Is.EqualTo(error));
    }

    [Test]
    public void ErrorResponse_WithErrorsAndData_IncludesBoth()
    {
        // Arrange
        var errors = new List<string> { "Validation error" };
        var data = new { Id = 123, Status = "Rejected" };

        // Act
        var response = ApiResponse<object>.ErrorResponse(errors, data);

        // Assert
        Assert.That(response.Success, Is.False);
        Assert.That(response.Data, Is.EqualTo(data));
        Assert.That(response.Errors, Is.EqualTo(errors));
    }

    [Test]
    public void ErrorResponse_WithSingleErrorAndData_IncludesBoth()
    {
        // Arrange
        var error = "Payment declined";
        var data = new { Id = 456, Status = "Declined" };

        // Act
        var response = ApiResponse<object>.ErrorResponse(error, data);

        // Assert
        Assert.That(response.Success, Is.False);
        Assert.That(response.Data, Is.EqualTo(data));
        Assert.That(response.Errors, Has.Count.EqualTo(1));
        Assert.That(response.Errors[0], Is.EqualTo(error));
    }

    [Test]
    public void ErrorResponse_WithEmptyErrorList_CreatesEmptyErrorsArray()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var response = ApiResponse<object>.ErrorResponse(errors);

        // Assert
        Assert.That(response.Success, Is.False);
        Assert.That(response.Errors, Is.Empty);
    }

    [Test]
    public void ApiResponse_DefaultConstructor_InitializesEmptyErrorsList()
    {
        // Act
        var response = new ApiResponse<object>();

        // Assert
        Assert.That(response.Errors, Is.Not.Null);
        Assert.That(response.Errors, Is.Empty);
        Assert.That(response.Success, Is.False); // Default is false
        Assert.That(response.Data, Is.Null);
    }

    [Test]
    public void ApiResponse_WithGenericType_WorksWithDifferentTypes()
    {
        // Arrange & Act
        var stringResponse = ApiResponse<string>.SuccessResponse("test");
        var intResponse = ApiResponse<int>.SuccessResponse(42);
        var guidResponse = ApiResponse<Guid>.SuccessResponse(Guid.NewGuid());

        // Assert
        Assert.That(stringResponse.Data, Is.EqualTo("test"));
        Assert.That(intResponse.Data, Is.EqualTo(42));
        Assert.That(guidResponse.Data, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public void ApiResponse_Properties_ArePubliclyAccessible()
    {
        // Arrange
        var response = new ApiResponse<object>
        {
            Success = true,
            Data = new { Test = "value" },
            Errors = new List<string> { "error" }
        };

        // Assert
        Assert.That(response.Success, Is.True);
        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Errors, Has.Count.EqualTo(1));
    }
}
