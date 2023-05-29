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
            state.Dependency = new WallCollisionJob().ScheduleParallel(state.Dependency);           
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance)]
        public partial struct WallCollisionJob : IJobEntity
        {
            private void Execute(ref LocalTransform transform, ref Velocity velocity)
            {
                float3 position = transform.Position;
                if (math.abs(position.x) > DataBurst.FieldSize.x * .5f)
                {
                    position.x = (DataBurst.FieldSize.x * .5f) *  math.sign(position.x);
                    velocity.Value.x *= -.5f;
                    velocity.Value.y *= .8f;
                    velocity.Value.z *= .8f;
                }

                if (math.abs(position.z) > DataBurst.FieldSize.z * .5f)
                {
                    position.z = (DataBurst.FieldSize.z * .5f) * math.sign(position.z);
                    velocity.Value.z *= -.5f;
                    velocity.Value.x *= .8f;
                    velocity.Value.y *= .8f;
                }

                if (math.abs(position.y) > DataBurst.FieldSize.y * .5f)
                {
                    position.y = (DataBurst.FieldSize.y * .5f) * math.sign(position.y);
                    velocity.Value.y *= -.5f;
                    velocity.Value.z *= .8f;
                    velocity.Value.x *= .8f;
                }

                transform.Position = position;
            }
        }

    }
}
