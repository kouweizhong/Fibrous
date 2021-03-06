﻿namespace Fibrous.Fibers
{
    using System;
    using Fibrous.Scheduling;

    /// <summary>
    /// Fiber that executes on caller's thread.  For testing and well understood situations.  Use with caution.
    /// </summary>
    public sealed class StubFiber : FiberBase
    {
        public StubFiber(IExecutor excecutor, IFiberScheduler scheduler)
            : base(excecutor, scheduler)
        {
        }

        public StubFiber(IExecutor executor) : base(executor)
        {
        }

        public StubFiber()
        {
        }

        protected override void InternalEnqueue(Action action)
        {
            //There is no lock here to force sequentiality, since that will cause deadlocks in some
            //situations.  Stub fibers are not thread safe.  
            Executor.Execute(action);
        }

        public static IFiber StartNew()
        {
            return Fiber.StartNew(FiberType.Stub);
        }

        public static IFiber StartNew(IExecutor executor)
        {
            return Fiber.StartNew(FiberType.Stub, executor);
        }
    }
}