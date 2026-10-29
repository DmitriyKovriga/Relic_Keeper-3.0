using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Scripts.Items.World;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static partial class RoomResolutionRuntimeCheck
    {
        public static void InspectFilterSubscribers()
        {
            var callbacks = typeof(LootFilterSettings).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as Action;
            var lines = callbacks?.GetInvocationList().Select(d =>
            {
                var target = d.Target as UnityEngine.Object;
                return d.Method.DeclaringType?.Name + "." + d.Method.Name + ": " +
                    (d.Target is UnityEngine.Object ? target == null ? "destroyed Unity target" : target.name : "managed target");
            }) ?? Array.Empty<string>();
            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "room-resolution-filter-subscribers.txt"), lines);
        }
    }
}
