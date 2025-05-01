using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

public class WraithProximityAnimEventChecks : MonoBehaviour
{
    public void DoShake()
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= 20)
        {
            ScreenShakeType shakeType = 0;

            // Determine shake intensity based on distance
            if (distance <= 5)
            {
                shakeType = ScreenShakeType.VeryStrong;
            }
            else if (distance <= 10)
            {
                shakeType = ScreenShakeType.Big;
            }
            else
            {
                shakeType = ScreenShakeType.Small;
            }

            HUDManager.Instance.ShakeCamera(shakeType);
        }
    }

    public void DoFear()
    {
        if (Vector3.Distance(transform.position, GameNetworkManager.Instance.localPlayerController.transform.position) < 16f)
        {
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f, true);
        }
    }
}