# combat-bees-benchmarks
Benchmarks of different versions of a simple bee simulation to compare the performance of different programming languages and game engines/frameworks

My 30 seconds frame benches (Ryzen 3900XT @ 4.3ghz, RX 6750XT):

(IN EDITOR)
Old DOTS: 4330

REMOVE LEG COMP - DOTS: 4650 + ~7%

ASM: Am dumb and don't know how to run it :)

Removing the LinkedEntityGroup component works since bees have no hierachy, and saves us 144 bytes per entity (bee) which is 14.4 MB saved for 100k bees.
It brings the Chunk entity count up from 46 to 80 ( 73 % increase), which gives us better memory ulitisation and slightly better performance. 
Note, this was 3 lines of code, so, very much worth it.


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
Less Memory + JobChunk - DOTS: 4750 + ~2%.

I think this is because the entities memory usage according to the profiler is ~24 MB, and my cpu has 64 MB of L3 cache, so it might provide a bigger memory saving for cpus with less cache.
Additionally it might also be caused by the fact that there are a lot of structural changes.


Now lets take a look at the attacksystem instead. Since it is one of the biggest time consumers, and one quick change is to move the check for target is dead, into the targeting system
where it probably should have been from the beginning. This is since it removes a branch from the attack system which can't be predicted, and the desired behaviour is for a bee to always have a target when attacking, which it now has.

Attack Job Chunk - DOTS: 4820 + ~1%


The biggest offender is adding / removing the "Alive" and "Dead" components, so if we can make the Alive component into an IEnableableComponent and use that to set entities as alive and dead, by toggling it on and off, we can prevent the structural changes from adding/removing these two components. This does however require a pretty big change in the codebase...


I'll try making a build of the DOTS version first, and check how big the performance delta between in editor and out of editor is.
NVM, can't make a build for some reason, it just crashes all the time, no errors. wack.. 64 bit build crashes, 32 bit build has like 40 fps...
EDITOR: 4820
BUILD: ???
