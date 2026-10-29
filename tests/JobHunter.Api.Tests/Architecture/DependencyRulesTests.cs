using System.Reflection;
using JobHunter.Api.Common.Persistence;
using JobHunter.Connectors.Abstractions;

namespace JobHunter.Api.Tests.Architecture;

public sealed class DependencyRulesTests
{
    private const string ApiAssembly = "JobHunter.Api";
    private const string AbstractionsAssembly = "JobHunter.Connectors.Abstractions";
    private const string BrowserAssembly = "JobHunter.Connectors.Browser";

    private static readonly string[] SharedConnectorAssemblies = [AbstractionsAssembly, BrowserAssembly];

    private static readonly string[] CoreNamespaces = ["JobHunter.Api.Features", "JobHunter.Api.Workers"];

    [Fact]
    public void Abstractions_DoesNotReferenceInfrastructure()
    {
        string[] forbidden = ["Microsoft.Playwright", "AngleSharp", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", ApiAssembly];

        var violations = ReferencedAssemblies(typeof(IJobConnector).Assembly)
            .Where(reference => forbidden.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)));

        Assert.Empty(violations);
    }

    [Fact]
    public void ConnectorAssemblies_DoNotReferenceHostOrDatabase()
    {
        string[] forbidden = [ApiAssembly, "Microsoft.EntityFrameworkCore", "Npgsql"];

        var violations =
            from assembly in ConnectorAssemblies().Append(Assembly.Load(BrowserAssembly))
            from reference in ReferencedAssemblies(assembly)
            where forbidden.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal))
            select $"{assembly.GetName().Name} -> {reference}";

        Assert.Empty(violations);
    }

    [Fact]
    public void ConnectorAssemblies_DoNotReferenceEachOther()
    {
        var connectors = ConnectorAssemblies().ToList();
        var connectorNames = connectors.Select(assembly => assembly.GetName().Name!).ToHashSet();

        var violations =
            from assembly in connectors
            from reference in ReferencedAssemblies(assembly)
            where connectorNames.Contains(reference)
            select $"{assembly.GetName().Name} -> {reference}";

        Assert.Empty(violations);
    }

    [Fact]
    public void FeaturesAndWorkers_DependOnlyOnConnectorAbstractions()
    {
        var connectorNames = ConnectorAssemblies().Select(assembly => assembly.GetName().Name).Append(BrowserAssembly).ToHashSet();

        var violations =
            from type in typeof(AppDbContext).Assembly.GetTypes()
            where CoreNamespaces.Any(ns => type.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true)
            from dependency in DependenciesOf(type)
            where connectorNames.Contains(dependency.Assembly.GetName().Name)
            select $"{type.FullName} -> {dependency.FullName}";

        Assert.Empty(violations.Distinct());
    }

    private static IEnumerable<string> ReferencedAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!);

    // Every assembly named JobHunter.Connectors.* is a platform adapter, except the shared contracts and browser infrastructure.
    private static IEnumerable<Assembly> ConnectorAssemblies() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "JobHunter.Connectors.*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !SharedConnectorAssemblies.Contains(name) && !name!.EndsWith(".Tests", StringComparison.Ordinal) && name != "JobHunter.Connectors.Testing")
            .Select(name => Assembly.Load(name!));

    private static IEnumerable<Type> DependenciesOf(Type type)
    {
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        IEnumerable<Type> signatures =
        [
            .. type.BaseType is null ? [] : new[] { type.BaseType },
            .. type.GetInterfaces(),
            .. type.GetFields(members).Select(field => field.FieldType),
            .. type.GetProperties(members).Select(property => property.PropertyType),
            .. type.GetConstructors(members).SelectMany(ctor => ctor.GetParameters()).Select(parameter => parameter.ParameterType),
            .. type.GetMethods(members).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)),
        ];

        return signatures.SelectMany(Flatten);
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
            return Flatten(type.GetElementType()!);

        return type.IsGenericType
            ? type.GetGenericArguments().SelectMany(Flatten).Prepend(type.GetGenericTypeDefinition())
            : [type];
    }
}
