using Microsoft.Extensions.DependencyInjection;
using WheelWizard.Shared.Services;

namespace WheelWizard.Test.Shared.Services;

public class ApiCallerTests
{
    public interface ITestApi
    {
        Task<string> TestEndpoint(string input);
    }

    [Fact(DisplayName = "Call api async with exception, returns failed result with said exception")]
    public async Task CallApiAsyncWithException_ReturnsFailedResultWithSaidException()
    {
        // Arrange
        var exception = new Exception("Test exception");

        var api = Substitute.For<ITestApi>();
        api.TestEndpoint(Arg.Any<string>()).Returns(Task.FromException<string>(exception));

        var apiCaller = CreateApiCaller(api);

        // Act
        var result = await apiCaller.CallApiAsync(testApi => testApi.TestEndpoint("testInput"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(exception, result.Error.Exception);
    }

    [Fact(DisplayName = "Call api async without exception, returns successful result")]
    public async Task CallApiAsyncWithoutException_ReturnsSuccessfulResult()
    {
        // Arrange
        const string testResult = "Test result";
        var api = Substitute.For<ITestApi>();
        api.TestEndpoint(Arg.Any<string>()).Returns(Task.FromResult(testResult));

        var apiCaller = CreateApiCaller(api);

        // Act
        var result = await apiCaller.CallApiAsync(testApi => testApi.TestEndpoint("testInput"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(testResult, result.Value);
    }

    private static IApiCaller<ITestApi> CreateApiCaller(ITestApi api)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTransient(typeof(IApiCaller<>), typeof(ApiCaller<>));
        serviceCollection.AddLogging();
        serviceCollection.AddTransient(_ => api);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var apiCaller = serviceProvider.GetRequiredService<IApiCaller<ITestApi>>();
        return apiCaller;
    }
}
