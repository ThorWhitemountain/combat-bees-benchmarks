using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst.CompilerServices;
using static UnityEngine.GraphicsBuffer;

namespace DOTS
{

    [BurstCompile]
    [UpdateBefore(typeof(AttackSystem))]
    [UpdateAfter(typeof(BeeWallCollisionSystem))]
    public partial struct TargetSystem : ISystem
    {
        private EntityQuery team1Alive;
        private EntityQuery team2Alive;

        //private ComponentLookup<Alive> aliveLookup;

        public void OnCreate(ref SystemState state)
        {
            team1Alive = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(Alive), typeof(Target), typeof(RandomComponent));
            team1Alive.AddSharedComponentFilter<Team>(1);
            team2Alive = state.EntityManager.CreateEntityQuery(typeof(Team), typeof(Alive), typeof(Target), typeof(RandomComponent));
            team2Alive.AddSharedComponentFilter<Team>(2);
            //aliveLookup = state.GetComponentLookup<Alive>(true);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var team1Entities = team1Alive.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var dep1);
            var team2Entities = team2Alive.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var dep2);

            //aliveLookup.Update(ref state);
            //state.Dependency = new TargetJob
            //{
            //    deltaTime = state.WorldUnmanaged.Time.DeltaTime,
            //    team1Enemies = team2Entities.AsDeferredJobArray(),
            //    team2Enemies = team1Entities.AsDeferredJobArray(),
            //    DeadLookup = aliveLookup,
            //}.ScheduleParallel(JobHandle.CombineDependencies(dep1, dep2));


            //Team1 job
            state.Dependency = new TeamTargetJob
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                teamEnemies = team2Entities.AsDeferredJobArray(),
            }.ScheduleParallel(team1Alive,JobHandle.CombineDependencies(dep1, dep2));


            //Team2 job
            state.Dependency = new TeamTargetJob
            {
                deltaTime = state.WorldUnmanaged.Time.DeltaTime,
                teamEnemies = team1Entities.AsDeferredJobArray(),
            }.ScheduleParallel(team2Alive, state.Dependency);

            team1Entities.Dispose(state.Dependency);
            team2Entities.Dispose(state.Dependency);
        }



        //Chose a random target
        [BurstCompile]
        public partial struct TeamTargetJob : IJobEntity
        {
            public float deltaTime;
            [ReadOnly]
            public NativeArray<Entity> teamEnemies;

            private void Execute(ref RandomComponent random, ref Target target)
            {
                // no target, or current target NOT alive.
                if (target.enemyTarget == Entity.Null)
                {
                    int newTarget = random.generator.NextInt(0, teamEnemies.Length);
                    target.enemyTarget = teamEnemies[newTarget];
                }
            }
        }




        //Chose a random target
        [BurstCompile]
        public partial struct TargetJob : IJobEntity
        {
            public float deltaTime;
            [ReadOnly]
            public NativeArray<Entity> team1Enemies;
            [ReadOnly]
            public NativeArray<Entity> team2Enemies;
            [ReadOnly]
            public ComponentLookup<Alive> DeadLookup;

            private void Execute(ref RandomComponent random, ref Target target, in Team team, in Alive _)
            {
                // no target, or current target NOT alive.
                if (target.enemyTarget == Entity.Null || !DeadLookup.IsComponentEnabled(target.enemyTarget))
                {
                    var enemies = team == 1 ? team1Enemies : team2Enemies;
                    int newTarget = random.generator.NextInt(0, enemies.Length);
                    target.enemyTarget = enemies[newTarget];
                }
            }
        }



        
    }
}
