﻿namespace Fibrous.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Fibrous.Channels;

    public class FiberKeyedCollection<TKey, T> : ISnapshotSubscriberPort<ItemAction<T>, T[]>, IRequestPort<Func<T, bool>, T[]>, IDisposable
    {
        private readonly Func<T, TKey> _keyGen;
        readonly IFiber _fiber;
        readonly Dictionary<TKey, T> _items = new Dictionary<TKey, T>();
        readonly IChannel<T> _add = new Channel<T>();
        readonly IChannel<T> _remove = new Channel<T>();
        readonly ISnapshotChannel<ItemAction<T>, T[]> _channel = new SnapshotChannel<ItemAction<T>, T[]>();
        readonly IRequestChannel<Func<T, bool>, T[]> _request = new RequestChannel<Func<T, bool>, T[]>();

        public FiberKeyedCollection(Func<T, TKey> keyGen)
        {
            _keyGen = keyGen;
            _fiber = Fiber.StartNew(FiberType.Pool);
            _channel.ReplyToPrimingRequest(_fiber, Reply);
            _add.Subscribe(_fiber, AddItem);
            _remove.Subscribe(_fiber, RemoveItem);
            _request.SetRequestHandler(_fiber, OnRequest);
        }

        public T this[TKey key]
        {
            set
            {
                bool exists = _items.ContainsKey(key);
                _items[key] = value;
                _channel.Publish(new ItemAction<T>(exists ? ActionType.Update : ActionType.Add, value));
            }
        }

        private void OnRequest(IRequest<Func<T, bool>, T[]> request)
        {
            request.Reply(_items.Values.Where(request.Request).ToArray());
        }

        private void RemoveItem(T obj)
        {
            var removed = _items.Remove(_keyGen(obj));
            if(removed)
                _channel.Publish(new ItemAction<T>(ActionType.Remove, obj));
        }

        private void AddItem(T obj)
        {
            var key = _keyGen(obj);
            bool exists = _items.ContainsKey(key);
            _items[key] = obj;
            _channel.Publish(new ItemAction<T>(exists ? ActionType.Update : ActionType.Add, obj));
        }

        public void Add(T item)
        {
            _add.Publish(item);
        }

        public void Remove(T item)
        {
            _remove.Publish(item);
        }

        private T[] Reply()
        {
            return _items.Values.ToArray();
        }

        public IDisposable Subscribe(IFiber fiber, Action<ItemAction<T>> receive, Action<T[]> receiveSnapshot)
        {
            return _channel.Subscribe(fiber, receive, receiveSnapshot);
        }

        public IDisposable SendRequest(Func<T, bool> request, IFiber fiber, Action<T[]> onReply)
        {
            return _request.SendRequest(request, fiber, onReply);
        }

        public IReply<T[]> SendRequest(Func<T, bool> request)
        {
            return _request.SendRequest(request);
        }

        public T[] GetItems(Func<T, bool> request)
        {
            return _request.SendRequest(request).Receive(TimeSpan.MaxValue).Value;
        }

        public void Dispose()
        {
            _fiber.Dispose();
        }
    }
}