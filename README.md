# combat-bees-benchmarks
Benchmarks of different versions of a simple bee simulation to compare the performance of different programming languages and game engines/frameworks

My 30 seconds frame benches (Ryzen 3900XT @ 4.3ghz, RX 6750XT):

(IN EDITOR)
Old DOTS: 4330
REMOVE LEG COMP - DOTS: 4650 + ~7%
ASM: ??

Removing the LinkedEntityGroup component works since bees have no hierachy, and saves us 144 bytes per entity (bee) which is 14.4 MB saved for 100k bees.
It brings the Chunk entity count up from 46 to 80 ( 73 % increase)


![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/fe7081d8-948c-40fc-9ee7-3e4b8ca4973f)
BeeMovementSystem takes 15.5 ms in total whish is the most demanding system, so I'll start with optimising that one.
AttackSystem takes 13.5 ms in total, so it's another demanding system to fix soon.

I'll start by converting the BeeMovementSystem job into an IJobChunk instead of IJobEntity

![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/0d30bc1e-a7f8-44f4-9bd3-f151cd1133a4)

It didn't make that large of a difference, but it's a beginning. It is however expected since IJobEntities gets compiled into an IJobChunk by unity, but it can have some overhead still.

However, each job gets a nativeArray of LocalToWorlds, which is 100 thousand LocalToWorlds, of 32 bytes each, which is 3.2 MB per job then. And using an array of entities would be 0.4MB instead, and then using random access via a componentlookup might still be faster due to a lot less memory usage. 
Especially  since each bee is currently doing a lookup into an array of 50000 LocalToWorlds... which is not very optimal

Additionally, by using a jobchunk we can schedule one job for each team. which means that dont have to send in both teams, and we dont need to do team checks for every entity.
So by doing this one change by splitting the job into a job per team, we remove the wrong array of LTWs (which removes 50k LTWs)
And we dont have to use the team component at all inside the job, which results in no longer needing to chose which array is our teammates inside the while loop. 

Implementing the change to pass in an array of all entities on this team and using a lookup for the position, is 0.2MB (50k * 4 bytes per entity), Didn't actually help as much as I wouldve thought.

But, now the 30 seconds frame benches is giving us a better score
Less Memory - DOTS: 4750 + ~2%.

I think this is because the entities memory usage according to the profiler is ~24 MB, and my cpu has 64 MB of L3 cache, so it might provide a bigger memory saving for cpus with less cache.
Additionally it might also be caused by the fact that there are a lot of structural changes, which I will take a look at now.

