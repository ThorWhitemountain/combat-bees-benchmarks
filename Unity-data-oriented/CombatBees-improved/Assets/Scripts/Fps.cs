
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class Fps : MonoBehaviour
{
    Text text;
    const string instructions = "Right Click + Drag: <i>Rotate camera</i> \n" +
                                "Scroll: <i>Zoom in/out</i>\n" +
                                "Left Click: <i>Spawn resources</i>\n" +
                                "R Key: <i>Restart</i>\n" +
                                "H Key: <i>Show/hide this info</i>\n";
    float timePassed = 0;
    float totalTimePassed = 0;
    int frameCount = 0;
    int framesAfter30Seconds = 0;

    public int warmupTimeSeconds = 3;
    // Start is called before the first frame update
    void Start()
    {
        text = GetComponentsInChildren<Text>()[0];
        QualitySettings.vSyncCount = 0;
    }

    // Update is called once per frame
    void Update()
    {

        if (Time.timeSinceLevelLoad < warmupTimeSeconds)
        {
            return;
        }

        timePassed += Time.unscaledDeltaTime;
        if (timePassed > 1)
        {
            var fps = frameCount / timePassed;
            text.text = instructions + "Fps: " + fps + "\n Number of bees: " + GetBeeCount() + " \nTotal frame count after 30 seconds: " + framesAfter30Seconds;
            frameCount = 0;
            timePassed = 0;
        }
        if (totalTimePassed <= 30.0f)
        {
            framesAfter30Seconds++;
        }
        totalTimePassed += Time.unscaledDeltaTime;
        frameCount++;
    }

    int GetBeeCount()
    {
        //int beeCount = 0;
        //if (BeeManager.instance)
        //{
        //    beeCount = BeeManager.instance.bees.Count;
        //}
        //else if (BeeSpawner.instance)
        //{
        //    beeCount = BeeSpawner.instance.teamsOfBees[0].Count + BeeSpawner.instance.teamsOfBees[1].Count;
        //}
        //else
        //{
        //    beeCount = Data.AliveCount[0] + Data.AliveCount[1] + Data.DeadCount[0] + Data.DeadCount[1];
        //}

        //if (beeCount == 0)
        //{
        return World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(Target)).CalculateEntityCount();
        //}
        //return beeCount;
    }
}
