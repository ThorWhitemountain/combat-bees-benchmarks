using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst.Intrinsics;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Burst.CompilerServices;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(BeePositionUpdateSystem))]
    public partial struct AttackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> localTransformLookup;
        private EntityQuery beeQuery;
        private ComponentTypeHandle<LocalTransform> transformHandle;
        private ComponentTypeHandle<Target> targetHandle;
        private ComponentTypeHandle<Velocity> velocityHandle;

        public void OnCreate(ref SystemState state)
        {
            beeQuery = state.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(Target), typeof(Velocity), typeof(Alive));
            velocityHandle = state.GetComponentTypeHandle<Velocity>(false);
            targetHandle = state.GetComponentTypeHandle<Target>(true);
            //Type handle for linear access for iterating
            transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            //Lookup for random access
            localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer.ParallelWriter ecb = GetEntityCommandBuffer(ref state);
            //Update the lookup instead of getting a new all the time.
            velocityHandle.Update(ref state);
            localTransformLookup.Update(ref state);
            transformHandle.Update(ref state);
            targetHandle.Update(ref state);
            //state.Dependency = new AttackJobChunk
            //{
            //    Ecb = ecb,
            //    deltaTime = state.WorldUnmanaged.Time.DeltaTime,
            //    TransformLookup = localTransformLookup
            //}.ScheduleParallel(state.Dependency);  


            state.Dependency = new AttackJobChunk
            {
                Ecb = ecb,
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                velocityHandle = velocityHandle,
                TransformLookup = localTransformLookup,
                targetHandle = targetHandle,
                transformHandle = transformHandle
            }.ScheduleParallel(beeQuery, state.Dependency);
        }

        private EntityCommandBuffer.ParallelWriter GetEntityCommandBuffer(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            return ecb.AsParallelWriter();
        }





        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public partial struct AttackJobChunk : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public float deltaTime;
            public ComponentTypeHandle<Velocity> velocityHandle;

            [ReadOnly]
            public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly]
            public ComponentTypeHandle<LocalTransform> transformHandle;
            [ReadOnly]
            public ComponentTypeHandle<Target> targetHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<LocalTransform> transforms = chunk.GetNativeArray(ref transformHandle);
                NativeArray<Velocity> velocities = chunk.GetNativeArray(ref velocityHandle);
                NativeArray<Target> targets = chunk.GetNativeArray(ref targetHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int i))
                {
                    Velocity velocity = velocities[i];
                    Target target = targets[i];

                    //RANDOM LOOKUP VERY DEMANDING
                    float3 delta = TransformLookup[target.enemyTarget].Position - transforms[i].Position;

                    float sqrDist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
                    //This seems to be true for almost all cases?
                    //Bees will most of the time not kill each frame, tell the compiler to optimise for entering the if statement.
                    if (Hint.Likely(sqrDist > Data.attackDistance * Data.attackDistance))
                    {
                        velocity.Value += delta.xyz * (Data.chaseForce * deltaTime / math.sqrt(sqrDist));
                        velocities[i] = velocity;
                        continue;
                    }
                    //else

                    velocity.Value += delta.xyz * (Data.attackForce * deltaTime / math.sqrt(sqrDist));
                    velocities[i] = velocity;
                    if (sqrDist < Data.hitDistance * Data.hitDistance)
                    {
                        Ecb.AddComponent<Dead>(i, target.enemyTarget);
                        Ecb.AddComponent(i, target.enemyTarget, new DeadTimer { time = 0.0f });
                        //Ecb.RemoveComponent<Alive>(chunkIndex, target.enemyTarget);
                        Ecb.RemoveComponent<Alive>(i, target.enemyTarget);
                    }
                }
            }
        }



        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public partial struct AttackJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public float deltaTime;
            [ReadOnly]
            public ComponentLookup<LocalToWorld> TransformLookup;

            private void Execute(Entity e, [ChunkIndexInQuery] int chunkIndex, ref Velocity velocity, ref Target target, in LocalTransform transform, in Alive _)
            {
                var beePosition = transform.Position;
                var enemyPosition = TransformLookup[target.enemyTarget].Position;

                var delta = enemyPosition - beePosition;
                float sqrDist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
                if (sqrDist > Data.attackDistance * Data.attackDistance)
                {
                    velocity.Value += delta * (Data.chaseForce * deltaTime / math.sqrt(sqrDist));
                }
                else
                {
                    velocity.Value += delta * (Data.attackForce * deltaTime / math.sqrt(sqrDist));
                    if (sqrDist < Data.hitDistance * Data.hitDistance)
                    {
                        Ecb.AddComponent<Dead>(chunkIndex, target.enemyTarget);
                        Ecb.AddComponent(chunkIndex, target.enemyTarget, new DeadTimer { time = 0.0f });
                        //Ecb.RemoveComponent<Alive>(chunkIndex, target.enemyTarget);
                        Ecb.RemoveComponent<Alive>(chunkIndex, target.enemyTarget);
                    }
                }
            }
        }


    }
}
