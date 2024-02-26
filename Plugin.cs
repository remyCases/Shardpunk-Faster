// Copyright (C) 2024 Rémy Cases
// See LICENSE file for extended copyright information.
// This file is part of the Speedshard repository from https://github.com/remyCases/Shardpunk-Faster.

using BepInEx;
using HarmonyLib;
using Assets.Scripts.UI.AnimationRoutines;
using Assets.Scripts.Logic;
using Assets.Scripts.Configuration;
using Assets.Scripts.GameUI;
using Assets.Scripts.GameUI.GameActions;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine;
using System;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;

namespace Faster;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        // Plugin startup logic
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        Harmony harmony = new(PluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }
}

[HarmonyPatch]
public static class Faster
{   
    // create button
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameplayMenuDisplayBehaviour), "Awake")]
    static void GameplayMenuDisplayBehaviourAwake(GameplayMenuDisplayBehaviour __instance)
    {
        GameObject prefab = GameObject.Find("BrightnessRoot");
        GameObject copied = UnityEngine.Object.Instantiate(prefab, __instance.transform.GetChild(0));
        copied.transform.SetSiblingIndex(4);
        NumericValueDropdownDisplayBehaviour button = copied.GetComponent<NumericValueDropdownDisplayBehaviour>();

        button.Text.text = "speedValue";
		button.Initialize(Enumerable.Range(2, 11).ToList<int>(), new Action<int>(GameplayMenuDisplayBehaviourUtils.OnSpeedLevelChanged));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SmoothMovementRoutine), "GetMoveTime")]
    static float GetMoveTime(float __result, SmoothMovementRoutine __instance)
    {
        FieldInfo character = AccessTools.Field(typeof(SmoothMovementRoutine), "_character");
        if (CombatUIState.Instance.IsQuickEnemyTurnEnabled && ((CombatCharacter) character.GetValue(__instance)).IsPlayerCharacter)
        {
            return __result / GameplayMenuDisplayBehaviourUtils.Speed;
        } 
        else if (CombatUIState.Instance.IsQuickEnemyTurnEnabled)
        {
            return __result * 2.0f / GameplayMenuDisplayBehaviourUtils.Speed;
        }
        return __result;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyTurnHeaderDisplayBehaviour), "OnQuickEnemyTurnButtonPerformed")]
    static void OnQuickEnemyTurnButtonPerformed(ref InputAction.CallbackContext ctx, EnemyTurnHeaderDisplayBehaviour __instance)
    {
        World instance = World.Instance;
        if (!instance.TurnsManager.EnemyTurnInProgress)
        {
            bool isFastForwardAToggle = GameConfiguration.Instance.IsFastForwardAToggle;
            bool flag = ctx.ReadValueAsButton();
            if (isFastForwardAToggle)
            {
                if (flag)
                {
                    CombatUIState.Instance.IsQuickEnemyTurnEnabled = !CombatUIState.Instance.IsQuickEnemyTurnEnabled;
                    return;
                }
            }
            else
            {
                CombatUIState.Instance.IsQuickEnemyTurnEnabled = flag;
            }
        
            FieldInfo _canvasGroup = AccessTools.Field(typeof(EnemyTurnHeaderDisplayBehaviour), "_canvasGroup");
            ((CanvasGroup)_canvasGroup.GetValue(__instance)).alpha = CombatUIState.Instance.IsQuickEnemyTurnEnabled ? 1.0f : 0.0f;
            __instance.transform.GetChild(0).GetComponent<TextMeshProUGUI>().alpha = 0.0f;
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyTurnHeaderDisplayBehaviour), "OnGameEvent")]
    static void OnGameEvent(object sender, EventArgs e, EnemyTurnHeaderDisplayBehaviour __instance)
    {
        TurnsManager turnsManager = World.Instance.TurnsManager;
        FieldInfo _canvasGroup = AccessTools.Field(typeof(EnemyTurnHeaderDisplayBehaviour), "_canvasGroup");
        ((CanvasGroup)_canvasGroup.GetValue(__instance)).alpha = CombatUIState.Instance.IsQuickEnemyTurnEnabled  || turnsManager.DisplayEnemyTurnText ? 1.0f : 0.0f;
        if (e is EnemyTurnStartedArgs && turnsManager.DisplayEnemyTurnText)
        {
            __instance.transform.GetChild(0).GetComponent<TextMeshProUGUI>().alpha = 1.0f;
            return;
        }
        if (e is EnemyTurnEndedArgs)
        {
            __instance.transform.GetChild(0).GetComponent<TextMeshProUGUI>().alpha = 0.0f;
        }
    }

    // move text
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "Start")]
    static void Start(EnemyTurnFastForwardTextDisplayBehaviour __instance)
    {
        // put texts above
        FieldInfo gp_tmpro = AccessTools.Field(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "FastForwardGamepadHintText");
        FieldInfo kb_tmpro = AccessTools.Field(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "FastForwardKeyboardHintText");
        ((TextMeshProUGUI)gp_tmpro.GetValue(__instance)).transform.position += new Vector3(0.0f, 4.0f, 0.0f);
        ((TextMeshProUGUI)kb_tmpro.GetValue(__instance)).transform.position += new Vector3(0.0f, 4.0f, 0.0f);
    }

    // change text display to show the current speed
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "Update")]
    static void Update(EnemyTurnFastForwardTextDisplayBehaviour __instance)
    {
        InputMode inputMode = GameConfiguration.Instance.InputMode;
        FieldInfo gp_tmpro = AccessTools.Field(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "FastForwardGamepadHintText");
        FieldInfo kb_tmpro = AccessTools.Field(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "FastForwardKeyboardHintText");
        if (inputMode != InputMode.KeyboardAndMouse)
        {
            ((TextMeshProUGUI)gp_tmpro.GetValue(__instance)).text = Regex.Replace(((TextMeshProUGUI)gp_tmpro.GetValue(__instance)).text, @"\sX\d\sspeed", string.Format(" X{0} speed", CombatUIState.Instance.IsQuickEnemyTurnEnabled ? GameplayMenuDisplayBehaviourUtils.Speed : 1));
        }
        else 
        {
            ((TextMeshProUGUI)kb_tmpro.GetValue(__instance)).text = Regex.Replace(((TextMeshProUGUI)kb_tmpro.GetValue(__instance)).text, @"\sX\d\sspeed", string.Format(" X{0} speed", CombatUIState.Instance.IsQuickEnemyTurnEnabled ? GameplayMenuDisplayBehaviourUtils.Speed : 1));
        }
    }

    // change text display to show the current speed
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "UpdateText")]
    static void UpdateText(EnemyTurnFastForwardTextDisplayBehaviour __instance)
    {
        InputMode inputMode = GameConfiguration.Instance.InputMode;
        FieldInfo gp_tmpro = AccessTools.Field(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "FastForwardGamepadHintText");
        FieldInfo kb_tmpro = AccessTools.Field(typeof(EnemyTurnFastForwardTextDisplayBehaviour), "FastForwardKeyboardHintText");
        if (inputMode != InputMode.KeyboardAndMouse)
        {
            ((TextMeshProUGUI)gp_tmpro.GetValue(__instance)).text += string.Format(" X{0} speed", CombatUIState.Instance.IsQuickEnemyTurnEnabled ? GameplayMenuDisplayBehaviourUtils.Speed : 1);
        }
        else 
        {
            ((TextMeshProUGUI)kb_tmpro.GetValue(__instance)).text += string.Format(" X{0} speed", CombatUIState.Instance.IsQuickEnemyTurnEnabled ? GameplayMenuDisplayBehaviourUtils.Speed : 1);
        }
    }
}

public static class GameplayMenuDisplayBehaviourUtils
{
    private static int speed;
    public static int Speed
    {
        get
        {
            return speed;
        }
        set
        {
            value = Mathf.Clamp(value, 2, 10);
            speed = value;
        }
    }

    public static void OnSpeedLevelChanged(int value)
    {
        Speed = speed;
    }
}