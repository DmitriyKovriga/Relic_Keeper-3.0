using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Inventory
{
    public readonly struct ItemDragDropContext
    {
        public string SourceEndpointId { get; }
        public InventoryItem Item { get; }
        public Vector2 PointerWorldPosition { get; }

        public ItemDragDropContext(string sourceEndpointId, InventoryItem item, Vector2 pointerWorldPosition)
        {
            SourceEndpointId = sourceEndpointId;
            Item = item;
            PointerWorldPosition = pointerWorldPosition;
        }
    }

    public interface IItemDragDropEndpoint
    {
        string EndpointId { get; }
        int Priority { get; }
        bool IsOpen { get; }
        bool IsPointerOver(Vector2 pointerWorldPosition);
        bool CanAccept(in ItemDragDropContext context);
        bool TryAccept(in ItemDragDropContext context);
    }

    public sealed class DelegateItemDragDropEndpoint : IItemDragDropEndpoint
    {
        private readonly Func<bool> _isOpen;
        private readonly Func<Vector2, bool> _isPointerOver;
        private readonly Func<ItemDragDropContext, bool> _canAccept;
        private readonly Func<ItemDragDropContext, bool> _tryAccept;

        public string EndpointId { get; }
        public int Priority { get; }

        public bool IsOpen => _isOpen == null || _isOpen();

        public DelegateItemDragDropEndpoint(
            string endpointId,
            int priority,
            Func<bool> isOpen,
            Func<Vector2, bool> isPointerOver,
            Func<ItemDragDropContext, bool> canAccept,
            Func<ItemDragDropContext, bool> tryAccept)
        {
            EndpointId = endpointId;
            Priority = priority;
            _isOpen = isOpen;
            _isPointerOver = isPointerOver;
            _canAccept = canAccept;
            _tryAccept = tryAccept;
        }

        public bool IsPointerOver(Vector2 pointerWorldPosition)
        {
            return _isPointerOver != null && _isPointerOver(pointerWorldPosition);
        }

        public bool CanAccept(in ItemDragDropContext context)
        {
            return _canAccept != null && _canAccept(context);
        }

        public bool TryAccept(in ItemDragDropContext context)
        {
            return _tryAccept != null && _tryAccept(context);
        }
    }

    public static class ItemDragDropService
    {
        private sealed class Registration : IDisposable
        {
            private readonly string _endpointId;
            private bool _disposed;

            public Registration(string endpointId)
            {
                _endpointId = endpointId;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Unregister(_endpointId);
            }
        }

        private sealed class Entry
        {
            public IItemDragDropEndpoint Endpoint;
            public long Sequence;
        }

        private static readonly object Sync = new object();
        private static readonly List<Entry> Endpoints = new List<Entry>();
        private static long _sequence;

        public static IDisposable Register(IItemDragDropEndpoint endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (string.IsNullOrWhiteSpace(endpoint.EndpointId))
                throw new ArgumentException("EndpointId must be non-empty.", nameof(endpoint));

            lock (Sync)
            {
                Endpoints.RemoveAll(e => e.Endpoint.EndpointId == endpoint.EndpointId);
                Endpoints.Add(new Entry
                {
                    Endpoint = endpoint,
                    Sequence = _sequence++
                });
            }

            return new Registration(endpoint.EndpointId);
        }

        public static bool TryDrop(string sourceEndpointId, InventoryItem item, Vector2 pointerWorldPosition)
        {
            if (item == null || item.Data == null) return false;

            List<Entry> snapshot;
            lock (Sync)
            {
                snapshot = new List<Entry>(Endpoints);
            }

            snapshot.Sort((a, b) =>
            {
                int byPriority = b.Endpoint.Priority.CompareTo(a.Endpoint.Priority);
                if (byPriority != 0) return byPriority;
                return a.Sequence.CompareTo(b.Sequence);
            });

            var context = new ItemDragDropContext(sourceEndpointId, item, pointerWorldPosition);
            foreach (var entry in snapshot)
            {
                var endpoint = entry.Endpoint;
                if (endpoint == null) continue;
                if (endpoint.EndpointId == sourceEndpointId) continue;
                if (!endpoint.IsOpen) continue;
                if (!endpoint.IsPointerOver(pointerWorldPosition)) continue;
                if (!endpoint.CanAccept(in context)) continue;
                if (endpoint.TryAccept(in context)) return true;
            }

            return false;
        }

        private static void Unregister(string endpointId)
        {
            if (string.IsNullOrWhiteSpace(endpointId)) return;
            lock (Sync)
            {
                Endpoints.RemoveAll(e => e.Endpoint.EndpointId == endpointId);
            }
        }
    }
}
