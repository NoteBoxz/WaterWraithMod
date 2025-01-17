using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;
using WaterWraithMod.Scripts;

public enum GameStle
{
    Pikmin4,
    Pikmin2,
}

[Serializable]
public class WaterWraithMeshOverride
{
    public Animator Anim = null!;
    public GameObject Object = null!;
    public MeshRenderer[] meshRenderers = [];
    public SkinnedMeshRenderer[] skinnedMeshRenderers = [];
    public GameObject[] TireObjectsToDisable = [];
    public GameObject[] TireObjectsToEnable = [];
    public AudioSource MoveAudioSource = null!;
}
public class WaterWraithMesh : MonoBehaviour
{
    public List<WaterWraithMeshOverride> overrides = new List<WaterWraithMeshOverride>();
    public WaterWraithAI AI = null!;
    int IndexBuffering = -1;

    public void SetOverride(int index)
    {
        if (AI.inSpecialAnimation)
        {
            IndexBuffering = index;
            StartCoroutine(WaitToSwitchOverride());
            return;
        }
        for (int i = 0; i < overrides.Count; i++)
        {
            WaterWraithMeshOverride waterWraithMeshOverride = overrides[i];
            if (i == index)
            {
                waterWraithMeshOverride.Object.SetActive(true);
                AI.meshRenderers = waterWraithMeshOverride.meshRenderers;
                AI.skinnedMeshRenderers = waterWraithMeshOverride.skinnedMeshRenderers;
                AI.creatureAnimator = waterWraithMeshOverride.Anim;
                AI.moveAud = waterWraithMeshOverride.MoveAudioSource;
                AI.TireObjectsToDisable = waterWraithMeshOverride.TireObjectsToDisable;
                AI.TireObjectsToEnable = waterWraithMeshOverride.TireObjectsToEnable;
            }
            else
            {
                waterWraithMeshOverride.Object.SetActive(false);
            }
        }
    }

    IEnumerator WaitToSwitchOverride()
    {
        WaterWraithMod.WaterWraithMod.Logger.LogInfo($"Buffing switch {IndexBuffering}");
        yield return new WaitUntil(() => !AI.inSpecialAnimation);

        for (int i = 0; i < overrides.Count; i++)
        {
            WaterWraithMeshOverride waterWraithMeshOverride = overrides[i];
            if (i == IndexBuffering)
            {
                waterWraithMeshOverride.Object.SetActive(true);
                AI.meshRenderers = waterWraithMeshOverride.meshRenderers;
                AI.skinnedMeshRenderers = waterWraithMeshOverride.skinnedMeshRenderers;
                AI.creatureAnimator = waterWraithMeshOverride.Anim;
                AI.moveAud = waterWraithMeshOverride.MoveAudioSource;
            }
            else
            {
                waterWraithMeshOverride.Object.SetActive(false);
            }
        }
    }

    [ContextMenu("AutoFind")]
    public void FindMeshes()
    {
        foreach (var item in overrides)
        {
            List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
            foreach (var go in item.Object.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderers.Add(go);
            }
            item.meshRenderers = meshRenderers.ToArray();
        }
        foreach (var item in overrides)
        {
            List<SkinnedMeshRenderer> meshRenderers = new List<SkinnedMeshRenderer>();
            foreach (var go in item.Object.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                meshRenderers.Add(go);
            }
            item.skinnedMeshRenderers = meshRenderers.ToArray();
        }
    }
}