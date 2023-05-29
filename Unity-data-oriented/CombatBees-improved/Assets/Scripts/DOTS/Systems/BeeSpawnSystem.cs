using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Core;
using static DOTS.BeePositionUpdateSystem;
using UnityEngine.XR;
using Unity.Collections;

namespace DOTS
{
    [BurstCompile]
    [UpdateBefore(typeof(DeadBeesSystem))]
    public partial struct BeeSpawnSystem : ISystem
    {
        private EntityQuery team1Alive;
        private EntityQuery team2Alive;
        private EntityQuery team1Dead;
        private EntityQuery team2Dead;
        private bool firstRun;

        public void OnCreate(ref SystemState state)
        {
            firstRun = true;
            team1Alive = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(Alive));
            team1Alive.AddSharedComponentFilter<Team>(1);
            team2Alive = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(Alive));
            team2Alive.AddSharedComponentFilter<Team>(2);


            EntityQueryDesc deadBeeDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(Team) },
                None = new ComponentType[] { typeof(Alive) }//Dead bees query as if they dont have the alive tag, since it's disables
            };

            team1Dead = state.EntityManager.CreateEntityQuery(deadBeeDesc);
            team1Dead.AddSharedComponentFilter<Team>(1);
            team2Dead = state.EntityManager.CreateEntityQuery(deadBeeDesc);
            team2Dead.AddSharedComponentFilter<Team>(2);



        }

        public void OnDestroy(ref SystemState state) { }


        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);

            if (firstRun)
            {

                if (!SystemAPI.TryGetSingleton(out Spawner spawner))
                {
                    //No spawner exists, so the subscene hasn't loaded in
                    return;
                }
                else
                {
                    firstRun = false;
                    //Remove the LEG from the bees, since they have no hierachy, which saves us 144 bytes per bee.
                    state.EntityManager.RemoveComponent<LinkedEntityGroup>(spawner.BlueBee);
                    state.EntityManager.RemoveComponent<LinkedEntityGroup>(spawner.YellowBee);


                    //Add all the required components to the bees prefab, so they dont get added for EVERY single bee that spawnws, lot of structural changes
                    state.EntityManager.AddComponentData(spawner.BlueBee, new Velocity());
                    state.EntityManager.AddComponentData(spawner.BlueBee, new Alive());
                    state.EntityManager.AddComponentData(spawner.BlueBee, new Target());
                    state.EntityManager.AddComponentData(spawner.BlueBee, new EntityPosition());
                    state.EntityManager.AddComponentData(spawner.BlueBee, new DeadTimer());
                    state.EntityManager.AddComponentData(spawner.BlueBee, new RandomComponent());
                    state.EntityManager.AddSharedComponent(spawner.BlueBee, new Team { Value = 1 });



                    state.EntityManager.AddComponentData(spawner.YellowBee, new Velocity());
                    state.EntityManager.AddComponentData(spawner.YellowBee, new Alive());
                    state.EntityManager.AddComponentData(spawner.YellowBee, new Target());
                    state.EntityManager.AddComponentData(spawner.YellowBee, new EntityPosition());
                    state.EntityManager.AddComponentData(spawner.YellowBee, new DeadTimer());
                    state.EntityManager.AddComponentData(spawner.YellowBee, new RandomComponent());
                    state.EntityManager.AddSharedComponent(spawner.YellowBee, new Team { Value = 2 });

                    state.EntityManager.Exists(spawner.YellowBee);
                }
            }

            int team1AliveCount = team1Alive.CalculateEntityCount();
            int team1DeadCount = team1Dead.CalculateEntityCount();
            int team1BeeCount = team1AliveCount + team1DeadCount;


            int team2AliveCount = team2Alive.CalculateEntityCount();
            int team2DeadCount = team2Dead.CalculateEntityCount();
            int team2BeeCount = team2AliveCount + team2DeadCount;

            // Creates a new instance of the job, assigns the necessary data, and schedules the job in parallel.
            new ProcessSpawnerJob
            {
                Ecb = ecb,
                team1BeeCount = team1BeeCount,
                team2BeeCount = team2BeeCount,
                timeData = state.WorldUnmanaged.Time

            }.ScheduleParallel();
        }

        private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            return ecb.AsParallelWriter();
        }


        [BurstCompile]
        public partial struct ProcessSpawnerJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public int team1BeeCount;
            public int team2BeeCount;
            public TimeData timeData;

            // IJobEntity generates a component data query based on the parameters of its `Execute` method.
            // This example queries for all Spawner components and uses `ref` to specify that the operation
            // requires read and write access. Unity processes `Execute` for each entity that matches the
            // component data query.
            private void Execute([ChunkIndexInQuery] int chunkIndex, ref Spawner spawner)
            {

                int beesToSpawnTeam1 = Data.beeStartCount / 2 - team1BeeCount;

                //Never bulk instatiate entities, by spawning one at a time.
                //Always bulk instantiate an array instead.
                NativeArray<Entity> team1Bees = new NativeArray<Entity>(beesToSpawnTeam1, Allocator.Temp);
                Ecb.Instantiate(chunkIndex, spawner.BlueBee, team1Bees);
                for (int i = 0; i < beesToSpawnTeam1; i++)
                {
                    //Entity newEntity = Ecb.Instantiate(chunkIndex, spawner.BlueBee);
                    Entity newEntity = team1Bees[i];
                    var rand = new RandomComponent();
                    rand.generator.InitState((uint)((i + 1) * (timeData.ElapsedTime + 1.0) * 57131));
                    var transform = LocalTransform.FromPosition(spawner.Team1SpawnPosition);
                    transform.Scale = rand.generator.NextFloat(Data.minBeeSize, Data.maxBeeSize);
                    Ecb.SetComponent(chunkIndex, newEntity, transform);
                    Ecb.SetComponent(chunkIndex, newEntity, rand);
                    //Ecb.AddComponent(chunkIndex, newEntity, new Velocity());
                    //Ecb.AddComponent(chunkIndex, newEntity, new Alive());
                    //Ecb.AddComponent(chunkIndex, newEntity, new Target());
                    //Ecb.AddComponent(chunkIndex, newEntity, new EntityPosition());
                    //Ecb.AddComponent(chunkIndex, newEntity, new DeadTimer());
                    //Ecb.AddSharedComponent(chunkIndex, newEntity, new Team { Value = 1 });
                }

                int beesToSpawnTeam2 = Data.beeStartCount / 2 - team2BeeCount;

                NativeArray<Entity> team2Bees = new NativeArray<Entity>(beesToSpawnTeam2, Allocator.Temp);
                Ecb.Instantiate(chunkIndex, spawner.YellowBee, team2Bees);

                

                for (int i = 0; i < beesToSpawnTeam2; i++)
                {
                    //Entity newEntity = Ecb.Instantiate(chunkIndex, spawner.YellowBee);
                    Entity newEntity = team2Bees[i];
                    var rand = new RandomComponent();
                    rand.generator.InitState((uint)((i + 1) * (timeData.ElapsedTime + 1.0) * 33223));
                    var transform = LocalTransform.FromPosition(spawner.Team2SpawnPosition);
                    transform.Scale = rand.generator.NextFloat(Data.minBeeSize, Data.maxBeeSize);
                    Ecb.SetComponent(chunkIndex, newEntity, transform);
                    Ecb.SetComponent(chunkIndex, newEntity, rand);
                    //Ecb.AddComponent(chunkIndex, newEntity, new Velocity());
                    //Ecb.AddComponent(chunkIndex, newEntity, new Alive());
                    //Ecb.AddComponent(chunkIndex, newEntity, new Target());
                    //Ecb.AddComponent(chunkIndex, newEntity, new EntityPosition());
                    //Ecb.AddComponent(chunkIndex, newEntity, new DeadTimer());
                    //Ecb.AddComponent(chunkIndex, newEntity, rand);
                    //Ecb.AddSharedComponent(chunkIndex, newEntity, new Team { Value = 2 });
                }
                team1Bees.Dispose();
                team2Bees.Dispose();
            }
        }
    }
}