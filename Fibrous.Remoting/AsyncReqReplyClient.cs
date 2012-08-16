namespace Fibrous.Remoting
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CrossroadsIO;
    using Fibrous.Channels;
    using Fibrous.Fibers;

    public class AsyncReqReplyClient<TRequest, TReply> : IAsyncRequestPort<TRequest, TReply>, IDisposable
    {
        private readonly byte[] _id = GetId();
        private readonly IAsyncRequestReplyChannel<TRequest, TReply> _internalChannel =
            new AsyncRequestReplyChannel<TRequest, TReply>();
        private readonly IFiber _fiber;
        private volatile bool _running = true;
        private readonly Context _replyContext;
        private readonly Socket _replySocket;
        private readonly Func<byte[], int, TReply> _replyUnmarshaller;
        private readonly Socket _requestSocket;
        private readonly Func<TRequest, byte[]> _requestMarshaller;
        private readonly Dictionary<Guid, IRequest<TRequest, TReply>> _requests =
            new Dictionary<Guid, IRequest<TRequest, TReply>>();

        private static byte[] GetId()
        {
            return Guid.NewGuid().ToByteArray();
        }

        public AsyncReqReplyClient(Context context,
                                   string address,
                                   int basePort,
                                   Func<TRequest, byte[]> requestMarshaller,
                                   Func<byte[], int, TReply> replyUnmarshaller,
                                   int bufferSize)
            : this(context, address, basePort, requestMarshaller, replyUnmarshaller, bufferSize, new PoolFiber())
        {
        }

        public AsyncReqReplyClient(Context context,
                                   string address,
                                   int basePort,
                                   Func<TRequest, byte[]> requestMarshaller,
                                   Func<byte[], int, TReply> replyUnmarshaller,
                                   int bufferSize,
                                   IFiber fiber)
        {
            _requestMarshaller = requestMarshaller;
            _replyUnmarshaller = replyUnmarshaller;
            _fiber = fiber;
            data = new byte[bufferSize];
            _internalChannel.SetRequestHandler(_fiber, OnRequest);
            //set up sockets and subscribe to pub socket
            _replyContext = context;
            _replySocket = _replyContext.CreateSocket(SocketType.SUB);
            _replySocket.Connect(address + ":" + (basePort + 1));
            _replySocket.Subscribe(_id);
            _requestSocket = _replyContext.CreateSocket(SocketType.PUSH);
            _requestSocket.Connect(address + ":" + basePort);
            _fiber.Start();
            Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
        }

        private readonly byte[] id = new byte[16];
        private readonly byte[] reqId = new byte[16];
        private readonly byte[] data;

        private void Run()
        {
            while (_running)
            {
                //check for time/cutoffs to trigger events...
                //   byte[] id = new byte[16];
                int idCount = _replySocket.Receive(id, TimeSpan.FromMilliseconds(100));
                if (idCount == -1)
                {
                    continue;
                }
                int reqIdCount = _replySocket.Receive(reqId, TimeSpan.FromSeconds(3));
                if (reqIdCount != 16)
                {
                    //ERROR
                    throw new Exception("Got id but no msg id");
                }
                var guid = new Guid(reqId);
                if (!_requests.ContainsKey(guid))
                {
                    throw new Exception("We don't have a msg SenderId for this reply");
                }
                int dataLength = _replySocket.Receive(data, TimeSpan.FromSeconds(3));
                if (dataLength == -1)
                {
                    //ERROR
                    throw new Exception("Got ids but no data");
                }
                TReply reply = _replyUnmarshaller(data, dataLength);
                _fiber.Enqueue(() => Send(guid, reply));
            }
            InternalDispose();
        }

        private void Send(Guid guid, TReply reply)
        {
            //TODO:  add check for request.
            IRequest<TRequest, TReply> request = _requests[guid];
            request.PublishReply(reply);
            _requests.Remove(guid);
        }

        private void InternalDispose()
        {
            _replySocket.Dispose();
            _requestSocket.Dispose();
            _fiber.Dispose();
        }

        private void OnRequest(IRequest<TRequest, TReply> obj)
        {
            //serialize and compress and send...
            byte[] msgId = GetId();
            _requests[new Guid(msgId)] = obj;
            _requestSocket.SendMore(_id);
            _requestSocket.SendMore(msgId);
            byte[] requestData = _requestMarshaller(obj.Request);
            _requestSocket.Send(requestData);
        }

        public IDisposable SendRequest(TRequest request, IFiber fiber, Action<TReply> onReply)
        {
            return _internalChannel.SendRequest(request, fiber, onReply);
        }

        public void Dispose()
        {
            _running = false;
        }
    }
}