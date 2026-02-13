using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;

namespace Scripts.Inventory
{
    /// <summary>
    /// Сетка слотов в стиле Path of Exile: каждая ячейка хранит ссылку на предмет или null.
    /// Многоклеточный предмет — одна и та же ссылка во всех занятых ячейках. Корень = верх-лево (минимальный индекс).
    /// </summary>
    public sealed class GridContainer
    {
        private readonly InventoryItem[] _grid;
        private readonly int _cols;
        private readonly int _rows;

        public int Cols => _cols;
        public int Rows => _rows;
        public int Length => _grid.Length;

        public GridContainer(int cols, int rows)
        {
            _cols = cols;
            _rows = rows;
            _grid = new InventoryItem[cols * rows];
        }

        public static void GetItemSize(InventoryItem item, int maxCols, int maxRows, out int w, out int h)
        {
            w = 1;
            h = 1;
            if (item?.Data == null) return;
            w = Mathf.Clamp(item.Data.Width, 1, maxCols);
            h = Mathf.Clamp(item.Data.Height, 1, maxRows);
        }

        private int Index(int col, int row) => row * _cols + col;

        /// <summary>Предмет в ячейке и корневой индекс (верх-лево). rootIndex = -1 если пусто.</summary>
        public void GetItemAt(int slotIndex, out InventoryItem item, out int rootIndex)
        {
            item = null;
            rootIndex = -1;
            if (slotIndex < 0 || slotIndex >= _grid.Length) return;
            item = _grid[slotIndex];
            if (item == null) return;
            int row = slotIndex / _cols;
            int col = slotIndex % _cols;
            // Реальный корень = верх-лево блока: идём влево и вверх, пока та же ссылка
            while (col > 0 && _grid[Index(col - 1, row)] == item) col--;
            while (row > 0 && _grid[Index(col, row - 1)] == item) row--;
            rootIndex = row * _cols + col;
        }

        public InventoryItem GetItemAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _grid.Length) return null;
            return _grid[slotIndex];
        }

        /// <summary>Можно ли поставить item с верх-левым углом в rootIndex.</summary>
        public bool CanPlace(InventoryItem item, int rootIndex)
        {
            if (item?.Data == null || rootIndex < 0) return false;
            GetItemSize(item, _cols, _rows, out int w, out int h);
            int rootCol = rootIndex % _cols;
            int rootRow = rootIndex / _cols;
            if (rootCol + w > _cols || rootRow + h > _rows) return false;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    int idx = rootIndex + r * _cols + c;
                    InventoryItem occupier = _grid[idx];
                    if (occupier != null && occupier != item) return false;
                }
            return true;
        }

        /// <summary>Убрать предмет из сетки (все ячейки с этой ссылкой).</summary>
        public void Remove(InventoryItem item)
        {
            if (item == null) return;
            for (int i = 0; i < _grid.Length; i++)
                if (_grid[i] == item)
                    _grid[i] = null;
        }

        /// <summary>Поставить item с корнем в rootIndex. Сначала убираем item из сетки. false если область занята другим предметом.</summary>
        public bool Place(InventoryItem item, int rootIndex)
        {
            if (item?.Data == null || rootIndex < 0) return false;
            GetItemSize(item, _cols, _rows, out int w, out int h);
            int rootCol = rootIndex % _cols;
            int rootRow = rootIndex / _cols;
            if (rootCol + w > _cols || rootRow + h > _rows) return false;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    int idx = rootIndex + r * _cols + c;
                    if (_grid[idx] != null && _grid[idx] != item) return false;
                }
            Remove(item);
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    _grid[rootIndex + r * _cols + c] = item;
            return true;
        }

        /// <summary>Забрать предмет по корневому индексу.</summary>
        public InventoryItem Take(int rootIndex)
        {
            GetItemAt(rootIndex, out InventoryItem item, out int _);
            if (item == null) return null;
            Remove(item);
            return item;
        }

        /// <summary>Уникальные предметы в прямоугольнике (rootCol, rootRow) размером w×h. Для Swap-if-One: если Count==1 — можно свопнуть.</summary>
        public HashSet<InventoryItem> GetUniqueItemsInArea(int rootCol, int rootRow, int w, int h)
        {
            var set = new HashSet<InventoryItem>();
            if (rootCol < 0 || rootRow < 0 || rootCol + w > _cols || rootRow + h > _rows) return set;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    int idx = (rootRow + r) * _cols + (rootCol + c);
                    if (_grid[idx] != null)
                        set.Add(_grid[idx]);
                }
            return set;
        }

        /// <summary>Уникальные предметы в области с корнем rootIndex и размером item.</summary>
        public HashSet<InventoryItem> GetUniqueItemsInAreaAtRoot(InventoryItem item, int rootIndex)
        {
            GetItemSize(item, _cols, _rows, out int w, out int h);
            int rootCol = rootIndex % _cols;
            int rootRow = rootIndex / _cols;
            return GetUniqueItemsInArea(rootCol, rootRow, w, h);
        }

        /// <summary>Первый свободный корень, куда влезает item. -1 если нет.</summary>
        public int FindFirstEmptyRoot(InventoryItem item, int excludeRoot = -1)
        {
            if (item?.Data == null) return -1;
            GetItemSize(item, _cols, _rows, out int w, out int h);
            for (int row = 0; row <= _rows - h; row++)
                for (int col = 0; col <= _cols - w; col++)
                {
                    int root = row * _cols + col;
                    if (root == excludeRoot) continue;
                    if (CanPlace(item, root)) return root;
                }
            return -1;
        }
    }
}
