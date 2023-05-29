using Unity.Entities;
using Unity.Burst;
using Unity.Transforms;
using UnityEngine;
using System.ComponentModel;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(BeePositionUpdateSystem))]
    public partial struct DeadBeesSystem : ISystem
    {

        private EntityQuery deadBeesQuery;


        public void OnCreate(ref SystemState state)
        {
            EntityQueryDesc deadBeeDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(DeadTimer), typeof(Velocity) },
                None = new ComponentType[] { typeof(Alive) }//Dead bees query as if they dont have the alive tag, since it's disables
            };
            deadBeesQuery = state.EntityManager.CreateEntityQuery(deadBeeDesc);
        }

        public void OnDestroy(ref SystemState state) { }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);

            //Move out the calculations that are the same for ALL entities, so they dont get computed again and again
            state.Dependency = new BeeDeadJob
            {
                Ecb = ecb,
                deltaTime = state.WorldUnmanaged.Time.DeltaTime / 10.0f,
                GravityDeltaTime = Field.gravity * state.WorldUnmanaged.Time.DeltaTime,
            }.ScheduleParallel(deadBeesQuery, state.Dependency);
        }

        private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            return ecb.AsParallelWriter();
        }

        [BurstCompile]
        public partial struct BeeDeadJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public float deltaTime;
            public float GravityDeltaTime;

            private void Execute(Entity e, [ChunkIndexInQuery] int chunkIndex, ref Velocity velocity, ref DeadTimer deadTimer)
            {
                deadTimer.time += deltaTime;
                velocity.Value.y += GravityDeltaTime;

                if (deadTimer.time >= 1.0f)
                {
                    Ecb.DestroyEntity(chunkIndex, e);
                }
            }
        }

    }
}
