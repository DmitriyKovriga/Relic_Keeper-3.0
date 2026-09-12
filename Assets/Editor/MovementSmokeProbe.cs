using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class MovementSmokeProbe
{
    private static Keyboard keyboard;
    private static double started;
    private static int phase = -1;
    private static PlayerMovement player;
    private static Scripts.Skills.SkillDataSO previousSkill;
    private static Scripts.Skills.PlayerSkillManager manager;
    private static Vector3 originalPosition;
    private static readonly StringBuilder report = new StringBuilder();
    private static readonly float[] durations = { .25f, .08f, .16f, .22f, .4f, .1f, .65f, .25f, .65f, .08f, .25f, .7f };
    private static readonly Key[][] keys = {
        new[]{Key.A}, new[]{Key.A,Key.LeftShift}, new[]{Key.A}, new[]{Key.D},
        Array.Empty<Key>(), new[]{Key.Space}, Array.Empty<Key>(), new[]{Key.S},
        Array.Empty<Key>(), new[]{Key.A,Key.LeftShift,Key.Space}, new[]{Key.A,Key.S}, Array.Empty<Key>()
    };
    static MovementSmokeProbe() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (keyboard == null)
        {
            if (!File.Exists("Library/MovementSmoke.request")) return;
            File.Delete("Library/MovementSmoke.request");
            player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null) return;
            originalPosition = player.transform.position;
            manager = player.GetComponent<Scripts.Skills.PlayerSkillManager>();
            previousSkill = manager.GetSkillData(5);
            var venom = Resources.Load<Scripts.Skills.SkillDataSO>("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill");
            typeof(Scripts.Skills.PlayerSkillManager).GetMethod("EquipSkill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(manager, new object[] { 5, venom });
            Debug.Log("Venom Strike runtime equip: " + (manager.GetSkillData(5) == venom));
            keyboard = InputSystem.AddDevice<Keyboard>("MovementSmokeKeyboard");
            started = EditorApplication.timeSinceStartup;
            report.AppendLine("time,phase,inputX,inputY,vx,vy,grounded,dash,immune,scaleX,scaleY,x,y");
        }
        double elapsed = EditorApplication.timeSinceStartup - started;
        int next = 0;
        double end = durations[0];
        while (elapsed >= end && next < durations.Length - 1) end += durations[++next];
        if (elapsed >= end)
        {
            InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            player.GetComponent<Rigidbody2D>().position = originalPosition;
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            typeof(Scripts.Skills.PlayerSkillManager).GetMethod("EquipSkill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(manager, new object[] { 5, previousSkill });
            File.WriteAllText("Library/MovementSmokeV2.csv", report.ToString());
            Debug.Log("Movement smoke probe complete: Library/MovementSmokeV2.csv");
            return;
        }
        if (phase != next)
        {
            phase = next;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[phase]));
        }
        var attack = player.GetComponent<PlayerAttackInput>();
        var visual = player.GetComponent<Scripts.Visuals.PlayerMovementVisual>();
        Vector3 scale = visual.DisplayRenderer.transform.localScale;
        report.AppendLine(FormattableString.Invariant($"{elapsed:F3},{phase},{player.CurrentMoveInput.x:F2},{player.CurrentMoveInput.y:F2},{player.CurrentVelocity.x:F3},{player.CurrentVelocity.y:F3},{player.IsGrounded},{attack.IsDashing},{attack.IsDamageImmune},{scale.x:F3},{scale.y:F3},{player.transform.position.x:F3},{player.transform.position.y:F3}"));
    }
}
