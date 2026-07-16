using Koras.Dataverse.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.Dataverse.UnitTests.DependencyInjection;

public class DependencyInjectionTests
{
    private static void ConfigureValid(DataverseClientOptions options)
    {
        options.EnvironmentUrl = ClientFactory.EnvironmentUrl;
        options.Authentication.UseTokenProvider(new ClientFactory.FakeTokenProvider());
    }

    [Fact]
    public void AddDataverse_resolves_a_singleton_client()
    {
        var services = new ServiceCollection();
        services.AddDataverse(ConfigureValid);
        using ServiceProvider provider = services.BuildServiceProvider();

        IDataverseClient first = provider.GetRequiredService<IDataverseClient>();
        IDataverseClient second = provider.GetRequiredService<IDataverseClient>();

        Assert.Same(first, second);
        Assert.IsType<DataverseClient>(first);
        Assert.NotNull(first.Metadata);
        Assert.NotNull(first.Solutions);
    }

    [Fact]
    public void Named_clients_resolve_through_the_factory()
    {
        var services = new ServiceCollection();
        services.AddDataverse("prod", ConfigureValid);
        services.AddDataverse("dev", ConfigureValid);
        using ServiceProvider provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDataverseClientFactory>();
        Assert.NotSame(factory.GetClient("prod"), factory.GetClient("dev"));
        Assert.Same(factory.GetClient("prod"), factory.GetClient("prod"));
    }

    [Fact]
    public void Unknown_names_fail_with_guidance()
    {
        var services = new ServiceCollection();
        services.AddDataverse("prod", ConfigureValid);
        using ServiceProvider provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDataverseClientFactory>();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.GetClient("missing"));
        Assert.Contains("prod", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Single_named_registration_backs_the_default_client()
    {
        var services = new ServiceCollection();
        services.AddDataverse("only", ConfigureValid);
        using ServiceProvider provider = services.BuildServiceProvider();

        IDataverseClient client = provider.GetRequiredService<IDataverseClient>();
        Assert.Same(provider.GetRequiredService<IDataverseClientFactory>().GetClient("only"), client);
    }

    [Fact]
    public void Multiple_named_registrations_make_the_default_ambiguous()
    {
        var services = new ServiceCollection();
        services.AddDataverse("a", ConfigureValid);
        services.AddDataverse("b", ConfigureValid);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IDataverseClient>());
    }

    [Fact]
    public void Invalid_options_fail_resolution()
    {
        var services = new ServiceCollection();
        services.AddDataverse(o => o.EnvironmentUrl = null);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.ThrowsAny<OptionsValidationException>(() => provider.GetRequiredService<IDataverseClient>());
    }

    [Fact]
    public void Http_environment_urls_are_rejected()
    {
        var options = new DataverseClientOptions { EnvironmentUrl = new Uri("http://insecure.example.com") };
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Retry_options_bounds_are_validated()
    {
        var options = new DataverseClientOptions { EnvironmentUrl = ClientFactory.EnvironmentUrl };
        options.Retry.MaxRetries = 11;
        Assert.Throws<InvalidOperationException>(options.Validate);

        options.Retry.MaxRetries = 3;
        options.Retry.MaxDelay = TimeSpan.Zero;
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Authentication_options_validate_required_fields()
    {
        var options = new DataverseClientOptions { EnvironmentUrl = ClientFactory.EnvironmentUrl };
        Assert.Throws<ArgumentException>(() => options.Authentication.UseClientSecret("", "c", "s"));

        options.Authentication.UseClientSecret("t", "c", "s");
        options.Validate(); // valid

        // Switching kinds resets prior state.
        options.Authentication.UseManagedIdentity();
        Assert.Null(options.Authentication.ClientSecret);
    }

    [Fact]
    public void AddDataverseHealthCheck_registers_the_probe()
    {
        var services = new ServiceCollection();
        services.AddDataverse(ConfigureValid);
        services.AddHealthChecks().AddDataverseHealthCheck(tags: new[] { "ready" });
        using ServiceProvider provider = services.BuildServiceProvider();

        var registrations = provider
            .GetRequiredService<IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>()
            .Value.Registrations;

        var registration = Assert.Single(registrations);
        Assert.Equal("dataverse", registration.Name);
        Assert.Contains("ready", registration.Tags);
    }
}
