using System;
using System.Collections.Generic;

namespace Scripts.Inventory
{
    public readonly struct ItemQuickTransferContext
    {
        public string SourceEndpointId { get; }
        public InventoryItem Item { get; }
        public bool IsShortcut { get; }

        public ItemQuickTransferContext(string sourceEndpointId, InventoryItem item, bool isShortcut)
        {
            SourceEndpointId = sourceEndpointId;
            Item = item;
            IsShortcut = isShortcut;
        }
    }

    public interface IItemQuickTransferEndpoint
    {
        string EndpointId { get; }
        int Priority { get; }
        bool IsOpen { get; }
        bool CanAccept(in ItemQuickTransferContext context);
        bool TryAccept(in ItemQuickTransferContext context);
    }

    public sealed class DelegateItemQuickTransferEndpoint : IItemQuickTransferEndpoint
    {
        private readonly Func<bool> _isOpen;
        private readonly Func<ItemQuickTransferContext, bool> _canAccept;
        private readonly Func<ItemQuickTransferContext, bool> _tryAccept;

        public string EndpointId { get; }
        public int Priority { get; }

        public bool IsOpen => _isOpen == null || _isOpen();

        public DelegateItemQuickTransferEndpoint(
            string endpointId,
            int priority,
            Func<bool> isOpen,
            Func<ItemQuickTransferContext, bool> canAccept,
            Func<ItemQuickTransferContext, bool> tryAccept)
        {
            EndpointId = endpointId;
            Priority = priority;
            _isOpen = isOpen;
            _canAccept = canAccept;
            _tryAccept = tryAccept;
        }

        public bool CanAccept(in ItemQuickTransferContext context)
        {
            return _canAccept != null && _canAccept(context);
        }

        public bool TryAccept(in ItemQuickTransferContext context)
        {
            return _tryAccept != null && _tryAccept(context);
        }
    }

    public static class ItemQuickTransferService
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
            public IItemQuickTransferEndpoint Endpoint;
            public long Sequence;
        }

        private static readonly object Sync = new object();
        private static readonly List<Entry> Endpoints = new List<Entry>();
        private static long _sequence;

        public static IDisposable Register(IItemQuickTransferEndpoint endpoint)
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

        public static bool TryQuickTransfer(string sourceEndpointId, InventoryItem item, bool isShortcut = true)
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

            var context = new ItemQuickTransferContext(sourceEndpointId, item, isShortcut);
            foreach (var entry in snapshot)
            {
                var endpoint = entry.Endpoint;
                if (endpoint == null) continue;
                if (endpoint.EndpointId == sourceEndpointId) continue;
                if (!endpoint.IsOpen) continue;
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
