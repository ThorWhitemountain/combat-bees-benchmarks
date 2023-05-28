using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(BeeMovementSystem))]
    public partial struct BeePositionUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new BeePositionUpdateJob
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);           
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
