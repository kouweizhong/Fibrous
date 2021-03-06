﻿namespace Fibrous
{
    using System;
    using Fibrous.Channels;

    /// <summary>
    /// Simple singleton point of event publishing by type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class EventBus<T>
    {
        public static readonly IChannel<T> Channel = new Channel<T>();

        public static IDisposable Subscribe(IFiber fiber, Action<T> receive)
        {
            return Channel.Subscribe(fiber, receive);
        }

        public static void Publish(T msg)
        {
            Channel.Publish(msg);
        }
    }
}