using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>
    /// Packed sorting sheet for corpse pieces. Each sprite gets a unique 4-order slot
    /// so SpriteMasks cannot clip another death, and later deaths are placed below.
    /// </summary>
    public sealed class EnemyDeathRemainsLayer
    {
        public const int SlotStride = 4;
        public const int FirstSpriteOrder = -4;
        public const int MaskBackOffset = -1;
        public const int OverlayOffset = 1;
        public const int MaskFrontOffset = 2;

        private int _nextSpriteOrder = FirstSpriteOrder;

        public int NextSpriteOrder => _nextSpriteOrder;

        public int AllocateSpriteOrder()
        {
            int order = _nextSpriteOrder;
            _nextSpriteOrder -= SlotStride;
            return order;
        }

        public static int MaskBackOrder(int spriteOrder) => spriteOrder + MaskBackOffset;
        public static int OverlayOrder(int spriteOrder) => spriteOrder + OverlayOffset;
        public static int MaskFrontOrder(int spriteOrder) => spriteOrder + MaskFrontOffset;
    }
}
