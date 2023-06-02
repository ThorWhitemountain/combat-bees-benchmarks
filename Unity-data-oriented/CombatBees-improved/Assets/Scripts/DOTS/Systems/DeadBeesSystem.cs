using Unity.Entities;
using Unity.Burst;
using Unity.Transforms;
using UnityEngine;
using Unity.Burst.Intrinsics;
using Unity.Collections;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(BeePositionUpdateSystem))]
    public partial struct DeadBeesSystem : ISystem
    {

        private EntityQuery deadBeesQuery;

        private ComponentTypeHandle<DeadTimer> deadHandle;
        private ComponentTypeHandle<Velocity> velocityHandle;
        private EntityTypeHandle type;
        public void OnCreate(ref SystemState state)
        {
            EntityQueryDesc deadBeeDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(DeadTimer), typeof(Velocity) },
                None = new ComponentType[] { typeof(Alive) }//Dead bees query as if they dont have the alive tag, since it's disables
            };
            deadBeesQuery = state.EntityManager.CreateEntityQuery(deadBeeDesc);
            deadHandle = state.GetComponentTypeHandle<DeadTimer>(false);
            velocityHandle = state.GetComponentTypeHandle<Velocity>(false);
            type = state.GetEntityTypeHandle();
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);

            //Move out the calculations that are the same for ALL entities, so they dont get computed again and again
            //state.Dependency = new BeeDeadJob
            //{
            //    Ecb = ecb,
            //    deltaTime = state.WorldUnmanaged.Time.DeltaTime / 10.0f,
            //    GravityDeltaTime = Field.gravity * state.WorldUnmanaged.Time.DeltaTime,
            //}.ScheduleParallel(deadBeesQuery, state.Dependency); 
            deadHandle.Update(ref state);
            velocityHandle.Update(ref state);
            type.Update(ref state);

            state.Dependency = new BeeDeadJobChunk
            {
                Ecb = ecb,
                deltaTime = state.WorldUnmanaged.Time.DeltaTime / 10.0f,
                GravityDeltaTime = Field.gravity * state.WorldUnmanaged.Time.DeltaTime,
                EntityType = type,
                deadHandle = deadHandle,
                velocityHandle = velocityHandle,
            }.ScheduleParallel(deadBeesQuery, state.Dependency);
        }

        private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            return ecb.AsParallelWriter();
        }



        [BurstCompile]
        public partial struct BeeDeadJobChunk : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public float deltaTime;
            public float GravityDeltaTime;

            public ComponentTypeHandle<Velocity> velocityHandle;
            public ComponentTypeHandle<DeadTimer> deadHandle;


            [ReadOnly]
            public EntityTypeHandle EntityType;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {

                var entities = chunk.GetNativeArray(EntityType);
                NativeArray<Velocity> velocities = chunk.GetNativeArray(ref velocityHandle);
                NativeArray<DeadTimer> deadTimers = chunk.GetNativeArray(ref deadHandle);


                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int i))
                {
                    DeadTimer deadTimer = deadTimers[i];
                    Velocity velocity = velocities[i];
                    deadTimer.time += deltaTime;
                    velocity.Value.y += GravityDeltaTime;

                    if (deadTimer.time >= 1)
                    {
                        Ecb.DestroyEntity(i, entities[i]);
                    }

                    deadTimers[i] = deadTimer;
                    velocities[i] = velocity;
                }
            }
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

                if (deadTimer.time >= 1)
                {
                    Ecb.DestroyEntity(chunkIndex, e);
                }
            }
        }






    }
}
