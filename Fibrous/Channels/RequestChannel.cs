namespace Fibrous.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public sealed class RequestChannel<TRequest, TReply> : IRequestChannel<TRequest, TReply>
    {
        private readonly IChannel<IRequest<TRequest, TReply>> _requestChannel =
            new Channel<IRequest<TRequest, TReply>>();

        public IDisposable SetRequestHandler(IFiber fiber, Action<IRequest<TRequest, TReply>> onRequest)
        {
            return _requestChannel.Subscribe(fiber, onRequest);
        }

        private sealed class ChannelRequest : IRequest<TRequest, TReply>, IReply<TReply>, IDisposable
        {
            private readonly object _lock = new object();
            //don't use a queue
            private readonly Queue<TReply> _resp = new Queue<TReply>();
            private bool _disposed;
            private bool _replied;

            public ChannelRequest(TRequest req)
            {
                Request = req;
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    _replied = true;
                    _disposed = true;
                    Monitor.PulseAll(_lock);
                }
            }

            public TRequest Request { get; }

            public void Reply(TReply response)
            {
                lock (_lock)
                {
                    if (_replied || _disposed) return;
                    _resp.Enqueue(response);
                    Monitor.PulseAll(_lock);
                }
            }

            public IResult<TReply> Receive(TimeSpan timeout)
            {
                lock (_lock)
                {
                    if (_replied)
                        return new Result<TReply>();
                    if (_resp.Count > 0)
                    {
                        _replied = true;
                        return new Result<TReply>(_resp.Dequeue());
                    }
                    if (_disposed)
                    {
                        _replied = true;
                        return new Result<TReply>();
                    }
                    //Max timespan throws an error here...
                    if (timeout == TimeSpan.MaxValue)
                        Monitor.Wait(_lock, -1);
                    else
                        Monitor.Wait(_lock, timeout);

                    if (_resp.Count > 0)
                    {
                        _replied = true;
                        return new Result<TReply>(_resp.Dequeue());
                    }
                }
                return new Result<TReply>();
            }
        }

        public IReply<TReply> SendRequest(TRequest request)
        {
            var channelRequest = new ChannelRequest(request);
            _requestChannel.Publish(channelRequest);
            return channelRequest;
        }

        public IDisposable SendRequest(TRequest request, IFiber fiber, Action<TReply> onReply)
        {
            var channelRequest = new AsyncChannelRequest(fiber, request, onReply);
            _requestChannel.Publish(channelRequest);
            return new Unsubscriber(channelRequest, fiber);
        }

        private class AsyncChannelRequest : IRequest<TRequest, TReply>, IDisposable
        {
            private readonly IChannel<TReply> _resp = new Channel<TReply>();
            private readonly IDisposable _sub;

            public AsyncChannelRequest(IFiber fiber, TRequest request, Action<TReply> replier)
            {
                Request = request;
                _sub = _resp.Subscribe(fiber, replier);
            }

            public void Dispose()
            {
                _sub?.Dispose();
            }

            public TRequest Request { get; }

            public void Reply(TReply response)
            {
                _resp.Publish(response);
            }
        }

        private struct Result<T> : IResult<T>
        {
            public Result(T value)
                : this()
            {
                Value = value;
                IsValid = true;
            }

            public bool IsValid { get; }
            public T Value { get; }
        }
    }
}