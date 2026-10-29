namespace JobHunter.Connectors.Abstractions;

public interface IJobConnector
{
    ConnectorDescriptor Descriptor { get; }
}
