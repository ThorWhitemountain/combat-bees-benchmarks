# combat-bees-benchmarks
Benchmarks of different versions of a simple bee simulation to compare the performance of different programming languages and game engines/frameworks

My 30 seconds frame benches (Ryzen 3900XT @ 4.3ghz, RX 6750XT):

(IN EDITOR)
Old DOTS: 4330
NEW(EST) DOTS : 5150 (+ 19%)

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


![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/f4d347fe-c694-45a5-925d-4350d451db1f)
![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/03f35e7e-e7c6-45b0-84c6-e366bcbefc05)
![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/5d0bee59-56c0-44b1-a065-86664c6df1d9)
![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/cbbbc1c2-a168-4ed6-93b5-515aa600a8a9)

BURST OFF
From this we can see that step 1 of the attacksystem is taking a very large amount of time, around 2/3rds of the total time
with step 2 taking around 1/3rd and step 3 barely registering.

BURST ON
![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/8f864926-5c37-48fd-9cc3-e8d63e79c7ca)
![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/d0cfab81-bdb7-4f31-90b9-16e9df054cfb)

By commenting out most of the code, we can see that the first lines of code still take a very long amount of time, considering that the total for the entire code is around 10-11ms, for the 
Burst Compiled step 1 to be taking 8.5 - 9.5ms, it's pretty rough.

I thought that the demanding part was the distance calculations, but it seems to be the random lookup being the most demanding part, as we're doing 100 thousand random localtransform lookups, which is bad for performance. 
By replacing the enemy position with (0,0,0) we can see how big this lookup cost is, by not having to pay it.

![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/54056a61-7555-4d48-88c0-07badedd7d8f)
Aaaand we get down to  0.5ms across all threads, which is like a ~95% reduction

Therefore, we really want to avoid making this randomlookup for every single bee, for every single frame.
And if we can't avoid paying this lookup cost, we want to make it cheaper if possible. 

![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/2622aaec-a4f4-41a0-99b9-18f4e001a1ab)
Swapping from the lookup being a LocalToWorld (64 bytes) to being a LocalTransform (32 bytes) makes the random lookup a bit cheaper, and brings the time for the system down to 8-9 ms, 
so it's not much of a save, and it might just be down to run variance or something. 
So we want to minimize the random lookup if possible, but the bees need to have an up to date enemytarget position every frame, so we can't make it update the target less often,
as we then change the behaviour.

Upon further investigation, the random access lookup I implemented in the movement job system wasn't worth it, as it also was very demanding as we were doing 2 random lookups for each bee. 
So now I've changed it back to use the large array of positions, but this time It's localTransforms instead of LocalToWorlds

Performance is still around the ~4800 frames

Since we know that reducing how much data has to read from this random access lookup can improve performance, I've created a new component, thats gets updated every frame (while we're already updating the transform components position) which only contains the position. 3 floats, 12 bytes.
Using this instead of localtransform to get the target positions in the movementsystem and the attacksystem, gives some pretty good performance improvements, and the entities per chunk count is still at 80.

30 second test
Position component - DOTS: 5150 + ~6.5%


But to avoid the random access lookup would require a design change, as having 100k bees doing 100k random lookups is not very good for performance...



Let's go fix those structural changes. The biggest problem is adding/removing dead and alive components all the time, so I've removed the Dead component, and made the Alive component be an IEnableable, so we just turn it on/off, causing no structual changes.
Additionally the adding of a new component, every time a bee dies is bad, so we make sure that bees get instantiated with a deadtimer, (since their deadtimer only gets processed, if they're NOT alive, this is fine).
Additionally, we add 6 NEW components and 1 SHARED component, for every bee we instantiate, which is also bad. So I made these components get added to the bee entity prefab on startup, so we just need to set values in these components instead.
Furthermore, all the bees are getting instantiated ONE at a time, which is not ideal, as when you're instantiating multiple bees, you should instantiate directly into a nativearray, which I've made it do now.
![image](https://github.com/ThorWhitemountain/combat-bees-benchmarks/assets/72937268/00c33c8c-296a-40b7-a979-f5a783a8cf60)


Now the only structural changes are caused from instantiating new bee arrays, and from destroying bees INDIVIDUALLY (which we could also make happen batch-based, by queuing them up via queue we fill from a job, and add to nativearray after the jobs execution, but I dont think enough bees are dying each frame to make this worth the hassle)

30 second test
Remove structual components - DOTS: 5330 + ~3%

The target system has been converted to one job for each team, which means we send in half the data (native array of entity) and we can remove a logical condition which should help the performance.
I have moved the random lookup for the alive component back into the attacksystem instead of the targetsystem, since it feels better to just have all the slow random lookups in the same system, as the target job is now around 1ms
