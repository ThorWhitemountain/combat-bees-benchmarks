using Unity.Entities;
using Unity.Mathematics;

public struct Target : IComponentData
{
    public Entity enemyTarget { get; set; }
}