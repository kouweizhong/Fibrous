﻿namespace Fibrous.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Fibrous.Channels;

    /// <summary>
    /// Collection class that can be monitored and provides a snapshot on subscription.  Can also be queried with a predicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FiberCollection<T> : ISnapshotSubscriberPort<ItemAction<T>, T[]>, IRequestPort<Func<T,bool>, T[]>, IDisposable
    {
        readonly IFiber _fiber;
        readonly List<T> _items = new List<T>();
        readonly IChannel<T> _add = new Channel<T>();
        readonly IChannel<T> _remove = new Channel<T>();
        readonly ISnapshotChannel<ItemAction<T>, T[]> _channel = new SnapshotChannel<ItemAction<T>, T[]>();
        readonly IRequestChannel<Func<T,bool>, T[]> _request = new RequestChannel<Func<T,bool>, T[]>();

        public FiberCollection()
        {
            _fiber = Fiber.StartNew(FiberType.Pool);
            _channel.ReplyToPrimingRequest(_fiber, Reply);
            _add.Subscribe(_fiber, AddItem);
            _remove.Subscribe(_fiber, RemoveItem);
            _request.SetRequestHandler(_fiber, OnRequest);
        }

        private void OnRequest(IRequest<Func<T,bool>, T[]> request)
        {
            request.Reply(_items.Where(request.Request).ToArray());
        }

        private void RemoveItem(T obj)
        {
            _items.Remove(obj);
            _channel.Publish(new ItemAction<T>(ActionType.Remove, obj));
        }

        private void AddItem(T obj)
        {
            _items.Add(obj);
            _channel.Publish(new ItemAction<T>(ActionType.Add, obj));
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
            return _items.ToArray();
        }

        public IDisposable Subscribe(IFiber fiber, Action<ItemAction<T>> receive, Action<T[]> receiveSnapshot)
        {
            return _channel.Subscribe(fiber, receive, receiveSnapshot);
        }

        public IDisposable SendRequest(Func<T,bool> request, IFiber fiber, Action<T[]> onReply)
        {
            return _request.SendRequest(request, fiber, onReply);
        }

        public IReply<T[]> SendRequest(Func<T,bool> request)
        {
            return _request.SendRequest(request);
        }

        public T[] GetItems(Func<T, bool> request)//, TimeSpan timout = TimeSpan.MaxValue)
        {
            return _request.SendRequest(request).Receive(TimeSpan.MaxValue).Value;
        }

        public void Dispose()
        {
            _fiber.Dispose();
        }
    }
}