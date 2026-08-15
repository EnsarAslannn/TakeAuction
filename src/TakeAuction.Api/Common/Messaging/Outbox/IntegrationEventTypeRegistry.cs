using System.Reflection;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class IntegrationEventTypeRegistry
{
    private readonly Dictionary<string, Type> _byName;

    public IntegrationEventTypeRegistry(Assembly assembly)
    {
        _byName = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(type => type.Namespace == ContractsNamespace)
            .ToDictionary(type => NameOf(type), type => type.AsType(), StringComparer.Ordinal);
    }

    public static string ContractsNamespace => typeof(Contracts.BidPlacedIntegrationEvent).Namespace!;

    public IReadOnlyCollection<string> KnownNames => _byName.Keys;

    public static string NameOf(Type type) => type.Name;

    public Type Resolve(string name) =>
        _byName.TryGetValue(name, out var type)
            ? type
            : throw new InvalidOperationException(
                $"Outbox message type '{name}' does not map to any contract in {ContractsNamespace}.");
}
