using UnityEngine;
using Unity.Entities;

class SpawnerAuthoring : MonoBehaviour
{
    public GameObject BlueBee;
    public GameObject YellowBee;
}

class SpawnerBaker : Baker<SpawnerAuthoring>
{
    public override void Bake(SpawnerAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.None);
        Entity blueBee = GetEntity(authoring.BlueBee, TransformUsageFlags.Dynamic);
        Entity yellowBee = GetEntity(authoring.YellowBee, TransformUsageFlags.Dynamic);
        AddComponent(entity, new Spawner
        {
            // By default, each authoring GameObject turns into an Entity.
            // Given a GameObject (or authoring component), GetEntity looks up the resulting Entity.
            BlueBee = blueBee,
            YellowBee = yellowBee,
            Team1SpawnPosition = DataBurst.Team1BeeSpawnPos,
            Team2SpawnPosition = DataBurst.Team2BeeSpawnPos
        });
    }
}
