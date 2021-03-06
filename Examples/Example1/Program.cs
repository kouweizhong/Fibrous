﻿namespace Example1
{
    using System;
    using Fibrous;
    using Fibrous.Agents;
    using Fibrous.Channels;
    using Fibrous.Fibers;

    internal class Program
    {
        private static void Main()
        {
            IChannel<string> toProcess = new Channel<string>();
            IChannel<string> completed = new Channel<string>();

            //first fiber will place something on channel for 2nd fiber to process
            using (IFiber fiber1 = PoolFiber.StartNew())
                //2nd fiber just writes to the completed channel
            using (IDisposable processor = new ChannelAgent<string>(toProcess, s => completed.Publish("Received " + s)))
                //A logger that watches the completed channel.  Could be optional
            using (IDisposable logger = new ChannelAgent<string>(completed, Console.WriteLine))
            {
                int count = 0;
                //Start sending a message after a second, every 2 seconds...
                fiber1.Schedule(() => toProcess.Publish("Test" + count++), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
                Console.ReadKey();
            }
        }
    }
}