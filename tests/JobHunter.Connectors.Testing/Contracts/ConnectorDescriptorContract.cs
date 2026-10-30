using JobHunter.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobHunter.Connectors.Testing.Contracts;

public abstract class ConnectorDescriptorContract
{
    protected abstract string ExpectedKey { get; }

    protected abstract void Register(IServiceCollection services);

    [Fact]
    public void Descriptor_Registered_HasExpectedKeyExactlyOnce()
    {
        using var provider = BuildProvider();

        var matches = provider.GetServices<IJobConnector>().Where(connector => connector.Descriptor.Key == ExpectedKey);

        Assert.Single(matches);
    }

    [Fact]
    public void Descriptor_Key_IsLowercaseSlug()
    {
        Assert.Matches("^[a-z0-9-]+$", Descriptor().Key);
    }

    [Fact]
    public void Descriptor_DisplayName_IsNotBlank()
    {
        Assert.False(string.IsNullOrWhiteSpace(Descriptor().DisplayName));
    }

    [Fact]
    public void Descriptor_DeclaredCapabilities_HaveKeyedServices()
    {
        using var provider = BuildProvider();
        var descriptor = Descriptor(provider);

        var missing = ConnectorCapabilityServices.Flags(descriptor.Capabilities)
            .Where(capability => KeyedService(provider, ConnectorCapabilityServices.ServiceTypeFor(capability)) is null);

        Assert.Empty(missing);
    }

    [Fact]
    public void Descriptor_KeyedServices_AreDeclaredAsCapabilities()
    {
        using var provider = BuildProvider();
        var descriptor = Descriptor(provider);

        var undeclared = ConnectorCapabilityServices.All
            .Where(serviceType => KeyedService(provider, serviceType) is not null)
            .Where(serviceType => !ConnectorCapabilityServices.CapabilitiesBackedBy(serviceType).Any(capability => descriptor.Capabilities.HasFlag(capability)))
            .Select(serviceType => serviceType.Name);

        Assert.Empty(undeclared);
    }

    [Fact]
    public void Descriptor_Traits_AreCoherent()
    {
        var traits = Descriptor().Traits;

        Assert.True(traits.DefaultMaxConcurrency >= 1, "DefaultMaxConcurrency must be at least 1");
        Assert.True(traits.DefaultScanInterval > TimeSpan.Zero, "DefaultScanInterval must be positive");
        Assert.True(traits.DefaultPolitenessDelayMin >= TimeSpan.Zero, "DefaultPolitenessDelayMin cannot be negative");
        Assert.True(traits.DefaultPolitenessDelayMin <= traits.DefaultPolitenessDelayMax, "DefaultPolitenessDelayMin must not exceed DefaultPolitenessDelayMax");
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        Register(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private ConnectorDescriptor Descriptor()
    {
        using var provider = BuildProvider();
        return Descriptor(provider);
    }

    private ConnectorDescriptor Descriptor(IServiceProvider provider) =>
        provider.GetServices<IJobConnector>().Single(connector => connector.Descriptor.Key == ExpectedKey).Descriptor;

    private object? KeyedService(IServiceProvider provider, Type serviceType) =>
        ((IKeyedServiceProvider)provider).GetKeyedService(serviceType, ExpectedKey);
}
