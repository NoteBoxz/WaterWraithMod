using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

[Serializable]
public class WraithProximityAnimEvent
{
    public bool CamShake;
    public ScreenShakeType screenShakeTypeNear;
    public float DistanceThresholdDistanceNear = 10;
    public ScreenShakeType screenShakeTypeFar;
    public float DistanceThresholdDistanceFar = 20;
    public float FearFactor = -1;
}
public class WraithProximityAnimEventChecks : MonoBehaviour
{
    public List<WraithProximityAnimEvent> animEvents = new List<WraithProximityAnimEvent>();

    public void DoCheck(int index)
    {
        WraithProximityAnimEvent animEvent = animEvents[index];
        PlayerControllerB player = StartOfRound.Instance.localPlayerController;
        if (animEvent.CamShake)
        {
            if (Vector3.Distance(player.transform.position, transform.position) < animEvent.DistanceThresholdDistanceNear)
            {
                HUDManager.Instance.ShakeCamera(animEvent.screenShakeTypeNear);
            }
            else if (Vector3.Distance(player.transform.position, transform.position) < animEvent.DistanceThresholdDistanceFar)
            {
                HUDManager.Instance.ShakeCamera(animEvent.screenShakeTypeFar);
            }
        }
        if (animEvent.FearFactor > 0)
        {
            if (Vector3.Distance(player.transform.position, transform.position) < animEvent.DistanceThresholdDistanceFar)
                player.JumpToFearLevel(animEvent.FearFactor, true);
        }
    }
}