namespace FlowForge.Shared.Primitives;

public abstract class AggregateRoot : Entity
{
    public uint Version { get; protected set; }
}
