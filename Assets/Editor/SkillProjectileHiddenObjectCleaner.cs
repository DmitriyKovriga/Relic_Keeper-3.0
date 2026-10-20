using Scripts.Skills.Projectiles;
using Scripts.Enemies;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SkillProjectileHiddenObjectCleaner
{
    static SkillProjectileHiddenObjectCleaner()
    {
        EditorApplication.delayCall += CleanupLeakedHiddenProjectiles;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Relic Keeper/Cleanup Hidden Skill Projectiles")]
    public static void CleanupLeakedHiddenProjectiles()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        SkillProjectile[] projectiles = Resources.FindObjectsOfTypeAll<SkillProjectile>();
        for (int i = 0; i < projectiles.Length; i++)
        {
            SkillProjectile projectile = projectiles[i];
            if (projectile == null || EditorUtility.IsPersistent(projectile))
                continue;

            GameObject go = projectile.gameObject;
            if (go == null)
                continue;

            bool hiddenRuntimeProjectile =
                go.hideFlags != HideFlags.None ||
                projectile.hideFlags != HideFlags.None ||
                go.name.Contains("SkillProjectile_RuntimeTemplate");

            if (!hiddenRuntimeProjectile)
                continue;

            Object.DestroyImmediate(go);
        }

        // Enemy templates are also runtime-only; never retain them across Play sessions.
        foreach (var projectile in Resources.FindObjectsOfTypeAll<EnemyProjectile>())
        {
            if (projectile == null || EditorUtility.IsPersistent(projectile)) continue;
            var go = projectile.gameObject;
            if (go.hideFlags != HideFlags.None && go.name.Contains("_ProjectileTemplate"))
                Object.DestroyImmediate(go);
        }
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += CleanupLeakedHiddenProjectiles;
    }
}
