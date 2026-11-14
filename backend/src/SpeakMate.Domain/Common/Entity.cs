namespace SpeakMate.Domain.Common;

/// <summary>Base type for aggregate roots and entities identified by a Guid.</summary>
public abstract class Entity
{
    protected Entity() => Id = Guid.CreateVersion7();

    protected Entity(Guid id) => Id = id;

    public Guid Id { get; private set; }
}
