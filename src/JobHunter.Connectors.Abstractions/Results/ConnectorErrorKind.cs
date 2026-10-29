namespace JobHunter.Connectors.Abstractions.Results;

public enum ConnectorErrorKind
{
    Transient,
    RateLimited,
    LayoutChanged,
    FormChanged,
    AuthenticationRequired,
    CaptchaDetected,
    PostingClosed,
    NotFound,
    InvalidInput,
    Unsupported,
}
