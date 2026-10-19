namespace SpendWise.Infrastructure.Banking;

// Chọn đúng host provider mà không hard-code môi trường trong Handler.
public enum SePayBankHubEnvironment { Sandbox, Production }

public sealed class SePayBankHubOptions
{
    public const string SectionName = "SePayBankHub";

    public bool Enabled { get; set; }
    public SePayBankHubEnvironment Environment { get; set; } = SePayBankHubEnvironment.Sandbox;
    public string SandboxBaseUrl { get; set; } = "https://bankhub-api-sandbox.sepay.vn";
    public string ProductionBaseUrl { get; set; } = "https://bankhub-api.sepay.vn";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? CompanyXid { get; set; }
    public string? LifecycleWebhookSecret { get; set; }
    public string? IpnApiKey { get; set; }
    public string? CompletionRedirectUri { get; set; }
    public string? MobileCompletionRedirectUri { get; set; }
    public string[] AllowedCompletionRedirectOrigins { get; set; } = [];
    public string[] AllowedIpRanges { get; set; } = [];
}
