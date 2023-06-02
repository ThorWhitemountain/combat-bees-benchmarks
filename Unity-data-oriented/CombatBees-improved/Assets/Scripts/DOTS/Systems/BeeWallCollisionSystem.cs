using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(BeePositionUpdateSystem))]
    public partial struct BeeWallCollisionSystem : ISystem
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new WallCollisionJob
            {
                fieldSize = DataBurst.FieldSize * 0.5f,

            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public partial struct WallCollisionJob : IJobEntity
        {
            public float3 fieldSize;

            private void Execute(ref LocalTransform transform, ref Velocity velocity)
            {
                float3 position = transform.Position;
                float3 currentVelocity = velocity.Value;

                if (math.abs(position.x) > fieldSize.x)
                {
                    position.x = fieldSize.x * math.sign(position.x);
                    currentVelocity *= new float3(-0.5f, 0.8f, 0.8f);
                }

                if (math.abs(position.z) > fieldSize.z)
                {
                    position.z = fieldSize.z * math.sign(position.z);
                    currentVelocity *= new float3(0.8f, 0.8f, -0.5f);
                }

                if (math.abs(position.y) > fieldSize.y)
                {
                    position.y = fieldSize.y * math.sign(position.y);
                    currentVelocity *= new float3(0.8f, -0.5f, 0.8f);
                }

                transform.Position = position;
                velocity.Value = currentVelocity;
            }
        }

    }
}
