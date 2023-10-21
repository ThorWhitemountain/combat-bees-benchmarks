using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst.Intrinsics;
using static DOTS.BeePositionUpdateSystem;
using UnityEngine.Profiling;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(AttackSystem))]
    public partial struct BeeMovementSystem : ISystem
    {

        private EntityQuery team1Bees;
        private EntityQuery team2Bees;
        private EntityQuery beeTeam1Query;
        private EntityQuery beeTeam2Query;

        //private SharedComponentTypeHandle<Team> teamHandle;
        private ComponentTypeHandle<LocalTransform> transformHandle;
        private ComponentTypeHandle<Velocity> velocityHandle;
        private ComponentTypeHandle<RandomComponent> randomHandle;


        public void OnCreate(ref SystemState state)
        {
            team1Bees = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(EntityPosition), typeof(Velocity), typeof(RandomComponent), typeof(Alive));
            team1Bees.AddSharedComponentFilter<Team>(1);
            team2Bees = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(EntityPosition), typeof(Velocity), typeof(RandomComponent), typeof(Alive));
            team2Bees.AddSharedComponentFilter<Team>(2);

            //Alive is needed to make sure we skip moving dead bees
            //Add a sharedComp filter to only allow one team for each job
            beeTeam1Query = state.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(RandomComponent), typeof(Velocity), typeof(Alive), typeof(Team));
            beeTeam1Query.AddSharedComponentFilter<Team>(1);
            beeTeam2Query = state.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(RandomComponent), typeof(Velocity), typeof(Alive), typeof(Team));
            beeTeam2Query.AddSharedComponentFilter<Team>(2);

            //teamHandle = state.GetSharedComponentTypeHandle<Team>();
            transformHandle = state.GetComponentTypeHandle<LocalTransform>(false);
            velocityHandle = state.GetComponentTypeHandle<Velocity>(false);
            randomHandle = state.GetComponentTypeHandle<RandomComponent>(false);

        }





        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var team1Transforms = team1Bees.ToComponentDataListAsync<EntityPosition>(Allocator.TempJob, state.Dependency, out var dep1);
            var team2Transforms = team2Bees.ToComponentDataListAsync<EntityPosition>(Allocator.TempJob, state.Dependency, out var dep2);

            //state.Dependency = new MovementJob
            //{
            //    deltaTime = state.WorldUnmanaged.Time.DeltaTime,
            //    Team1Transforms = team1Transforms.AsDeferredJobArray(),
            //    Team2Transforms = team2Transforms.AsDeferredJobArray(),

            //}.ScheduleParallel(JobHandle.CombineDependencies(dep1, dep2));

            //Update the handles so they are up to date
            //teamHandle.Update(ref state);
            transformHandle.Update(ref state);
            randomHandle.Update(ref state);
            velocityHandle.Update(ref state);

            //The team component is shared, so only entities of the same team, exist in the same chunk. So all chunks are team sorted.
            //Team1 job
            state.Dependency = new MovementJobChunk
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                //Only get this teams positions
                allyPositions = team1Transforms.AsDeferredJobArray(),
                transformHandle = transformHandle,
                randomHandle = randomHandle,
                velocityHandle = velocityHandle,
            }.ScheduleParallel(beeTeam1Query, JobHandle.CombineDependencies(dep1, dep2));


            //Team2 job
            state.Dependency = new MovementJobChunk
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                //Only get this teams positions
                allyPositions = team2Transforms.AsDeferredJobArray(),
                transformHandle = transformHandle,
                randomHandle = randomHandle,
                velocityHandle = velocityHandle,
            }.ScheduleParallel(beeTeam2Query, state.Dependency);

            team1Transforms.Dispose(state.Dependency);
            team2Transforms.Dispose(state.Dependency);
        }





        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public partial struct MovementJobChunk : IJobChunk
        {
            public float deltaTime;

            public ComponentTypeHandle<LocalTransform> transformHandle;
            public ComponentTypeHandle<Velocity> velocityHandle;
            public ComponentTypeHandle<RandomComponent> randomHandle;

            [ReadOnly]
            public NativeArray<EntityPosition> allyPositions;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<LocalTransform> transforms = chunk.GetNativeArray(ref transformHandle);
                NativeArray<Velocity> velocities = chunk.GetNativeArray(ref velocityHandle);
                NativeArray<RandomComponent> randoms = chunk.GetNativeArray(ref randomHandle);

                float teamRepulsionFactor = Data.teamRepulsion * deltaTime;
                float rotationLerpFactor = deltaTime * 4;
                float teamAttraction = Data.teamAttraction * deltaTime;
                float dampingFactor = Data.damping * deltaTime;
                float jitterFactor = Data.flightJitter * deltaTime;

                //Same team for all bees in the while loop
                int aliveBeesCount = allyPositions.Length;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out int i))
                {
                    RandomComponent random = randoms[i];
                    Velocity velocity = velocities[i];
                    LocalTransform transform = transforms[i];

                    float3 randomVector;
                    randomVector.x = random.generator.NextFloat() * 2.0f - 1.0f;
                    randomVector.y = random.generator.NextFloat() * 2.0f - 1.0f;
                    randomVector.z = random.generator.NextFloat() * 2.0f - 1.0f;

                    velocity.Value += randomVector * jitterFactor;
                    velocity.Value *= 1f - dampingFactor;

                        
                    //Move towards random ally
                    float3 beePosition = transform.Position;
                    //Get a random entitiy, and then get its LocalToWorld component
                    int allyIndex = random.generator.NextInt(aliveBeesCount-1);

                    //RANDOM LOOKUP VERY DEMANDING
                    float3 allyPosition = allyPositions[allyIndex].Position;
                    float3 delta = allyPosition - beePosition;

                    float dist = math.sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
                    dist = math.max(0.01f, dist);
                    velocity.Value += delta * (teamAttraction / dist);

                    //Move away from random ally
                    //allyIndex = random.generator.NextInt(aliveBeesCount);
                    //RANDOM LOOKUP VERY DEMANDING
                    // last allyindex + 1 since it's next to the last bee, can give better memory speeds, since it's in the just loaded cache line
                    allyPosition = allyPositions[allyIndex+1].Position;

                    delta = allyPosition - beePosition;
                    dist = math.sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
                    dist = math.max(0.011f, dist);

                    velocity.Value -= delta * (teamRepulsionFactor / dist);
                    quaternion targetRotation = quaternion.LookRotation(math.normalize(velocity.Value), new float3(0, 1, 0));
                    transform.Rotation = math.nlerp(transform.Rotation, targetRotation, rotationLerpFactor);

                    //save changes to components
                    transforms[i] = transform;
                    randoms[i] = random;
                    velocities[i] = velocity;
                }
            }
        }

        [BurstCompile]
        public partial struct MovementJob : IJobEntity
        {
            public float deltaTime;
            [ReadOnly] public NativeArray<LocalToWorld> Team1Transforms;
            [ReadOnly] public NativeArray<LocalToWorld> Team2Transforms;

            //Alive is needed to make sure we skip moving dead bees
            private void Execute(ref LocalTransform transform, ref Velocity velocity, ref RandomComponent random, in Team team, in Alive _)
            {
                float3 randomVector;
                randomVector.x = random.generator.NextFloat() * 2.0f - 1.0f;
                randomVector.y = random.generator.NextFloat() * 2.0f - 1.0f;
                randomVector.z = random.generator.NextFloat() * 2.0f - 1.0f;

                velocity.Value += randomVector * (Data.flightJitter * deltaTime);
                velocity.Value *= (1f - Data.damping * deltaTime);

                var aliveBeesCount = team == 1 ? Team1Transforms.Length : Team2Transforms.Length;
                var allyPositions = team == 1 ? Team1Transforms : Team2Transforms;
                //Move towards random ally
                float3 beePosition = transform.Position;
                int allyIndex = random.generator.NextInt(aliveBeesCount);
                var allyPosition = allyPositions[allyIndex].Position;
                float3 delta = allyPosition - beePosition;
                float dist = math.sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
                dist = math.max(0.01f, dist);
                velocity.Value += delta * (Data.teamAttraction * deltaTime / dist);

                //Move away from random ally
                allyIndex = random.generator.NextInt(aliveBeesCount);
                allyPosition = allyPositions[allyIndex].Position;
                delta = allyPosition - beePosition;
                dist = math.sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
                dist = math.max(0.011f, dist);
                velocity.Value -= delta * (Data.teamRepulsion * deltaTime / dist);

                var rotation = transform.Rotation;
                var targetRotation = quaternion.LookRotation(math.normalize(velocity.Value), new float3(0, 1, 0));
                rotation = math.nlerp(rotation, targetRotation, deltaTime * 4);
                transform.Rotation = rotation;
            }
        }



    }
}
