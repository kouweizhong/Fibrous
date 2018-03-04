namespace Fibrous.Experimental
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Fibrous.Fibers;
    using Fibrous.Queues;

    public sealed class SpinLockPoolFiber : FiberBase
    {
        private readonly TaskFactory _taskFactory;
        private bool _flushPending;
        private List<Action> _queue = new List<Action>(1024 * 32);
        private List<Action> _toPass = new List<Action>(1024 * 32);
        private SpinLock _spinLock = new SpinLock(false);
        public SpinLockPoolFiber(IExecutor config, TaskFactory taskFactory)
            : base(config)
        {
            _taskFactory = taskFactory;
        }

        public SpinLockPoolFiber(IExecutor executor)
            : this(executor, new TaskFactory(TaskCreationOptions.PreferFairness, TaskContinuationOptions.None))
        {
        }

        public SpinLockPoolFiber(TaskFactory taskFactory)
            : this(new Executor(), taskFactory)
        {
        }

        public SpinLockPoolFiber()
            : this(new Executor(), new TaskFactory(TaskCreationOptions.PreferFairness, TaskContinuationOptions.None))
        {
        }

        protected override void InternalEnqueue(Action action)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                _queue.Add(action);
                if (!_flushPending)
                {
                    _taskFactory.StartNew(Flush);
                    _flushPending = true;
                }
            }
            finally
            {
                if(lockTaken) _spinLock.Exit();
            }
        }

        private void Flush()
        {
            List<Action> toExecute = ClearActions();
            if (toExecute.Count > 0)
            {
                Executor.Execute(toExecute);
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    if (_queue.Count > 0)
                    {
                        // don't monopolize thread.
                        _taskFactory.StartNew(Flush);
                    }
                    else
                        _flushPending = false;
                }
                finally
                {
                    if(lockTaken) _spinLock.Exit();
                }
            }
        }

        private List<Action> ClearActions()
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                if (_queue.Count == 0)
                {
                    _flushPending = false;
                    return Queue.Empty;
                }
                Lists.Swap(ref _queue, ref _toPass);
                _queue.Clear();
                return _toPass;
            }
            finally
            {
                if (lockTaken) _spinLock.Exit();
            }
        }

        public static IFiber StartNew()
        {
            var fiber = new SpinLockPoolFiber();
            fiber.Start();
            return fiber;
        }

        public static IFiber StartNew(IExecutor exec)
        {
            var fiber = new SpinLockPoolFiber(exec);
            fiber.Start();
            return fiber;
        }
    }
}