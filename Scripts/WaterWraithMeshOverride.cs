using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;
using WaterWraithMod.Scripts;

public class WaterWraithMeshOverride : MonoBehaviour
{
    public Animator Anim = null!;
    public GameObject Object = null!;
    public MeshRenderer[] meshRenderers = [];
    public SkinnedMeshRenderer[] skinnedMeshRenderers = [];
    public AudioSource MoveAudioSource = null!;
    public GameObject BaseColider = null!, PikminColider = null!;
}