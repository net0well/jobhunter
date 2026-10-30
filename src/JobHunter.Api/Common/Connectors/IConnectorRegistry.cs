using System.Diagnostics.CodeAnalysis;
using JobHunter.Connectors.Abstractions;

namespace JobHunter.Api.Common.Connectors;

internal interface IConnectorRegistry
{
    IReadOnlyList<ConnectorDescriptor> Connectors { get; }

    ConnectorDescriptor? Find(string key);

    bool TryGet<TCapability>(string key, [NotNullWhen(true)] out TCapability? capability)
        where TCapability : class;
}
