using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Results;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Tests.Connectors;

public sealed class ConnectorErrorMapperTests
{
    [Theory]
    [InlineData(ConnectorErrorKind.Transient, ErrorType.Unavailable)]
    [InlineData(ConnectorErrorKind.RateLimited, ErrorType.Unavailable)]
    [InlineData(ConnectorErrorKind.NotFound, ErrorType.NotFound)]
    [InlineData(ConnectorErrorKind.InvalidInput, ErrorType.Validation)]
    [InlineData(ConnectorErrorKind.PostingClosed, ErrorType.Conflict)]
    [InlineData(ConnectorErrorKind.FormChanged, ErrorType.Conflict)]
    [InlineData(ConnectorErrorKind.LayoutChanged, ErrorType.Unprocessable)]
    [InlineData(ConnectorErrorKind.AuthenticationRequired, ErrorType.Unprocessable)]
    [InlineData(ConnectorErrorKind.CaptchaDetected, ErrorType.Unprocessable)]
    [InlineData(ConnectorErrorKind.Unsupported, ErrorType.Unprocessable)]
    internal void ToError_EachKind_MapsToExpectedErrorType(ConnectorErrorKind kind, ErrorType expected)
    {
        var error = new ConnectorError(kind, "Connector.Code", "Something happened").ToError();

        Assert.Equal(expected, error.Type);
    }

    [Fact]
    public void ToError_KeepsConnectorCodeAndMessage()
    {
        var error = new ConnectorError(ConnectorErrorKind.RateLimited, "Connector.Paused", "Paused until noon").ToError();

        Assert.Equal("Connector.Paused", error.Code);
        Assert.Equal("Paused until noon", error.Message);
    }
}
