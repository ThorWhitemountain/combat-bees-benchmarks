using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(BeeMovementSystem))]
    public partial struct BeePositionUpdateSystem : ISystem
    {

        private EntityQuery bees;
        private ComponentTypeHandle<LocalTransform> transformHandle;
        private ComponentTypeHandle<EntityPosition> positionHandle;
        private ComponentTypeHandle<Velocity> velocityHandle;
        public void OnCreate(ref SystemState state)
        {
            bees = state.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(Velocity));
            transformHandle = state.GetComponentTypeHandle<LocalTransform>(false);
            positionHandle = state.GetComponentTypeHandle<EntityPosition>(false);
            velocityHandle = state.GetComponentTypeHandle<Velocity>(true);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //state.Dependency = new BeePositionUpdateJob
            //{
            //    deltaTime = state.WorldUnmanaged.Time.DeltaTime
            //}.ScheduleParallel(state.Dependency);

            transformHandle.Update(ref state);
            velocityHandle.Update(ref state);
            positionHandle.Update(ref state);

            state.Dependency = new BeePositionUpdateJobChunk
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                transformHandle = transformHandle,
                velocityHandle = velocityHandle,
                positionHandle = positionHandle,
            }.ScheduleParallel(bees, state.Dependency);
        }


        public struct EntityPosition : IComponentData
        {
            public float3 Position;
        }


        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public partial struct BeePositionUpdateJobChunk : IJobChunk
        {

            [ReadOnly]
            public float deltaTime;

            public ComponentTypeHandle<LocalTransform> transformHandle;
            public ComponentTypeHandle<EntityPosition> positionHandle;

            [ReadOnly]
            public ComponentTypeHandle<Velocity> velocityHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<LocalTransform> transforms = chunk.GetNativeArray(ref transformHandle);
                NativeArray<EntityPosition> positions = chunk.GetNativeArray(ref positionHandle);
                NativeArray<Velocity> velocities = chunk.GetNativeArray(ref velocityHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out int i))
                {
                    LocalTransform transform = transforms[i];
                    Velocity velocity = velocities[i];
                    transform.Position += velocity.Value * deltaTime;
                    transforms[i] = transform;
                    positions[i] = new EntityPosition { Position = transform.Position };
                }
            }
        }

        [BurstCompile]
        public partial struct BeePositionUpdateJob : IJobEntity
        {
            public float deltaTime;

            private void Execute(ref LocalTransform transform, in Velocity velocity)
            {
                transform.Position += velocity.Value * deltaTime;
            }
        }
    }
}
