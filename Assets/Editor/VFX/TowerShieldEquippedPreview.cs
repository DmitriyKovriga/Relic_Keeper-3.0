using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Items;
using Scripts.Inventory;
using Scripts.Skills;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    /// <summary>Captures the real equipped hero in the open scene, with no inventory or save changes.</summary>
    public static class TowerShieldEquippedPreview
    {
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var hero = Object.FindFirstObjectByType<WeaponVisualController>();
            if (hero == null) throw new InvalidOperationException("No active equipped hero in the open scene.");
            var tower = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(TowerShieldChargeBuilder.TowerPath);
            var inventory = InventoryManager.Instance;
            if (inventory == null) throw new InvalidOperationException("No live equipment inventory.");
            var originalEquipment = inventory.EquipmentItems;
            var refresh = typeof(WeaponVisualController).GetMethod("RefreshVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
            string folder = Path.Combine(Path.GetTempPath(), "tower-shield-equipped");
            Directory.CreateDirectory(folder);
            var preview = new GameObject("Temporary equipped shield camera").AddComponent<Camera>();
            var target = new RenderTexture(480, 270, 24);
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Vector3 originalScale = hero.transform.localScale;
            try
            {
                // Render through the real equipment/stance controller without firing save or equipment events.
                inventory.EquipmentItems = (InventoryItem[])originalEquipment.Clone();
                var item = new InventoryItem(tower);
                item.GrantedSkills.Add(AssetDatabase.LoadAssetAtPath<SkillDataSO>(TowerShieldChargeBuilder.SkillPath));
                inventory.EquipmentItems[(int)EquipmentSlot.OffHand] = item;
                if (inventory.EquipmentItems[(int)EquipmentSlot.MainHand]?.Data is WeaponItemSO main && main.IsTwoHanded)
                    inventory.EquipmentItems[(int)EquipmentSlot.MainHand] = null;
                refresh.Invoke(hero, null);
                var held = hero.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(s => s.enabled && s.sprite == tower.InHandSprite);
                if (held == null) throw new InvalidOperationException("Tower shield did not appear through the real equipment controller.");
                if (Camera.main != null) preview.CopyFrom(Camera.main);
                preview.enabled = false;
                preview.orthographic = true;
                preview.orthographicSize = 270f / 48f;
                preview.aspect = 480f / 270f;
                preview.transform.position = hero.transform.position + new Vector3(0, 0.2f, -10);
                preview.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                preview.GetUniversalAdditionalCameraData().volumeLayerMask = 0;
                target.Create();
                foreach (int facing in new[] { 1, -1 })
                {
                    hero.transform.localScale = new Vector3(Mathf.Abs(originalScale.x) * facing, originalScale.y, originalScale.z);
                    RenderPipeline.SubmitRenderRequest(preview, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, facing == 1 ? "Right.png" : "Left.png"), image.EncodeToPNG());
                }
                File.WriteAllText(Path.Combine(folder, "Report.txt"), $"Actual equipped hero: {hero.name}; renderer: {held.name}; local position: {held.transform.localPosition}; flipX: {held.flipX}; sprite: {held.sprite.name}. Native 24 PPU, both facing directions, live scene lighting.");
                Debug.Log("Equipped tower shield captured in both directions: " + folder);
            }
            finally
            {
                inventory.EquipmentItems = originalEquipment;
                refresh.Invoke(hero, null);
                hero.transform.localScale = originalScale;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(preview.gameObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
