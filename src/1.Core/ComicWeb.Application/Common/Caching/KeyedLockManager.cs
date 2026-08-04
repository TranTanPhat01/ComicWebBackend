using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Caching;

public sealed class KeyedLockManager
{
    private readonly ConcurrentDictionary<string, KeyedLock> _locks = new();

    public async Task<IDisposable> LockAsync(string key)
    {
        while (true)
        {
            var keyedLock = _locks.GetOrAdd(key, _ => new KeyedLock());
            lock (keyedLock)
            {
                if (keyedLock.Disposed) continue;
                keyedLock.RefCount++;
            }

            await keyedLock.Semaphore.WaitAsync();
            return new LockReleaser(key, keyedLock, this);
        }
    }

    private void Release(string key, KeyedLock keyedLock)
    {
        keyedLock.Semaphore.Release();
        lock (keyedLock)
        {
            keyedLock.RefCount--;
            if (keyedLock.RefCount == 0)
            {
                keyedLock.Disposed = true;
                _locks.TryRemove(key, out _);
                keyedLock.Semaphore.Dispose();
            }
        }
    }

    private sealed class KeyedLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount { get; set; }
        public bool Disposed { get; set; }
    }

    private sealed class LockReleaser : IDisposable
    {
        private readonly string _key;
        private readonly KeyedLock _keyedLock;
        private readonly KeyedLockManager _manager;
        private bool _disposed;

        public LockReleaser(string key, KeyedLock keyedLock, KeyedLockManager manager)
        {
            _key = key;
            _keyedLock = keyedLock;
            _manager = manager;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _manager.Release(_key, _keyedLock);
                _disposed = true;
            }
        }
    }
}
