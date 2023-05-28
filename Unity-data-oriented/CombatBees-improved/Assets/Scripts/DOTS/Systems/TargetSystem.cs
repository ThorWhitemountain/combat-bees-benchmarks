using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(AttackSystem))]
    [UpdateAfter(typeof(BeeWallCollisionSystem))]
    public partial struct TargetSystem : ISystem
    {
        private EntityQuery team1Alive;
        private EntityQuery team2Alive;

        private ComponentLookup<Dead> deadLookup;
       


        public void OnCreate(ref SystemState state)
        {
            team1Alive = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(Alive));
            team1Alive.AddSharedComponentFilter<Team>(1);
            team2Alive = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(Alive));
            team2Alive.AddSharedComponentFilter<Team>(2);
            deadLookup = state.GetComponentLookup<Dead>(true);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var team1Entities = team1Alive.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var dep1);
            var team2Entities = team2Alive.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var dep2);

            deadLookup.Update(ref state);
            state.Dependency = new TargetJob
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                team1Enemies = team2Entities.AsDeferredJobArray(),
                team2Enemies = team1Entities.AsDeferredJobArray(),
                DeadLookup = deadLookup,
            }.ScheduleParallel(JobHandle.CombineDependencies(dep1, dep2));

            team1Entities.Dispose(state.Dependency);
            team2Entities.Dispose(state.Dependency);
        }

        //Chose a random target
        [BurstCompile]
        public partial struct TargetJob : IJobEntity
        {
            public float deltaTime;
            [ReadOnly] public NativeArray<Entity> team1Enemies;
            [ReadOnly] public NativeArray<Entity> team2Enemies;
            [ReadOnly]
            public ComponentLookup<Dead> DeadLookup;
            private void Execute(ref RandomComponent random, ref Target target, in Team team, in Alive _)
            {
                // no target, or current target dead.
                if (target.enemyTarget == Entity.Null || DeadLookup.HasComponent(target.enemyTarget))
                {
                    var enemies = team == 1 ? team1Enemies : team2Enemies;
                    int newTarget = random.generator.NextInt(0, enemies.Length);
                    target.enemyTarget = enemies[newTarget];
                }
            }
        }
    }
}
