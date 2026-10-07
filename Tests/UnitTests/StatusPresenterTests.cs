using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.UnitTests;

public sealed class StatusPresenterTests
{
    [Fact]
    public void PresentsEveryOverallStateWithAnExplicitSafeAction()
    {
        var presenter = new StatusPresenter();

        foreach (var status in Enum.GetValues<HealthStatus>())
        {
            var presentation = presenter.Present(Snapshot(status));

            Assert.Contains(ExpectedTitleLabel(status), presentation.Title);
            Assert.False(string.IsNullOrWhiteSpace(presentation.Summary));
            Assert.False(string.IsNullOrWhiteSpace(presentation.RecommendedAction));
            Assert.Contains("Connected Clients: N/A (not tracked)", presentation.Details);
        }
    }

    [Fact]
    public void RendersDegradedAndNotCheckedComponentsWithoutInjectedText()
    {
        var snapshot = Snapshot(HealthStatus.Running) with
        {
            Bridge = new HealthComponent(
                "bridge",
                HealthComponentStatus.Degraded,
                requiredForOverall: false,
                summary: "C:\\Users\\Alice\\private.gdb token=secret",
                errorCode: "SECRET_VALUE"),
            Compatibility = new HealthComponent(
                "compatibility",
                HealthComponentStatus.NotChecked,
                summary: "payload: /private/path")
        };

        var presentation = new StatusPresenter().Present(snapshot);
        var text = presentation.ToDisplayText();

        Assert.Contains("Bridge: Degraded", presentation.Details);
        Assert.Contains("Compatibility: NotChecked", presentation.Details);
        Assert.Contains("degraded", presentation.RecommendedAction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Alice", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private.gdb", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET_VALUE", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownClientIdentifiersUseAnOpaqueSafeLabel()
    {
        var snapshot = Snapshot(HealthStatus.Running) with
        {
            Clients = new[]
            {
                new ClientHealthFact(
                    "C:\\Users\\Alice\\client.json",
                    HealthComponentStatus.NotChecked,
                    optional: true)
            }
        };

        var text = new StatusPresenter().Present(snapshot).ToDisplayText();

        Assert.Contains("Client External client: NotChecked", text);
        Assert.DoesNotContain("Alice", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client.json", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorFallbackIsFixedAndDoesNotExposeExceptionText()
    {
        var presentation = StatusPresenter.UnavailableFallback();
        var text = presentation.ToDisplayText();

        Assert.Contains("Unavailable", presentation.Title);
        Assert.Contains("N/A", presentation.Details);
        Assert.DoesNotContain("Exception", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplaysOnlyTheCanonicalEndpointAndSafeDeclaredVersionFacts()
    {
        var snapshot = Snapshot(HealthStatus.Running) with
        {
            ProductIdentity = "1.0.2",
            Server = new TransportHealthFacts(
                TransportHealthStatus.Running,
                "127.0.0.1",
                6520,
                "/mcp"),
            Compatibility = new HealthComponent(
                "compatibility",
                HealthComponentStatus.DeclaredOnly,
                declaredTarget: "3.5.0")
        };

        var text = new StatusPresenter().Present(snapshot).ToDisplayText();

        Assert.Contains("Endpoint: http://127.0.0.1:6520/mcp", text);
        Assert.Contains("Add-in: 1.0.2", text);
        Assert.Contains("Declared target: Pro 3.5.0 / live compatibility not checked", text);
        Assert.Contains("live map/project not probed", text);
    }

    [Fact]
    public void RejectsInjectedEndpointAndVersionValuesFromThePresentation()
    {
        var snapshot = Snapshot(HealthStatus.Running) with
        {
            ProductIdentity = @"C:\\Users\\Alice\\private.json",
            Server = new TransportHealthFacts(
                TransportHealthStatus.Running,
                @"C:\\Users\\Alice\\private.json",
                6520,
                "/private-payload"),
            Compatibility = new HealthComponent(
                "compatibility",
                HealthComponentStatus.DeclaredOnly,
                summary: "secret",
                declaredTarget: @"C:\\Users\\Alice\\private.gdb")
        };

        var text = new StatusPresenter().Present(snapshot).ToDisplayText();

        Assert.Contains("Endpoint: not displayed", text);
        Assert.Contains("Add-in: not displayed", text);
        Assert.Contains("Compatibility: DeclaredOnly", text);
        Assert.DoesNotContain("Alice", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
    }

    private static HealthSnapshot Snapshot(HealthStatus status)
        => new()
        {
            OverallStatus = status,
            Server = new TransportHealthFacts(
                status == HealthStatus.Running
                    ? TransportHealthStatus.Running
                    : status == HealthStatus.Stopped
                        ? TransportHealthStatus.Stopped
                        : TransportHealthStatus.Unknown),
            ArcGISHost = HealthComponent.NotChecked("arcgisHost", requiredForOverall: false),
            Configuration = new HealthComponent("configuration", HealthComponentStatus.Pass),
            Compatibility = new HealthComponent("compatibility", HealthComponentStatus.Pass),
            Bridge = HealthComponent.NotChecked("bridge", requiredForOverall: false),
            Logging = HealthComponent.NotChecked("logging", requiredForOverall: false)
        };

    private static string ExpectedTitleLabel(HealthStatus status)
        => status switch
        {
            HealthStatus.BridgeFailure => "Bridge Failure",
            HealthStatus.MalformedConfiguration => "Malformed Configuration",
            _ => status.ToString()
        };
}
