﻿namespace Fibrous.Tests
{
    using System;
    using System.Diagnostics;
    using NUnit.Framework;

    public class ActionFactory<T>
    {
        private readonly Action<T> _on;

        public ActionFactory(Action<T> onMsg)
        {
            _on = onMsg;
        }

        public Action Create(T msg)
        {
            return () => _on(msg);
        }

        public Action CreateObject(object obj)
        {
            return () => _on((T)obj);
        }

        public static Action Create(T msg, Action<T> target)
        {
            return () => target(msg);
        }
    }

    [TestFixture]
    public class PerfBug
    {
        public static Action CreateString(string msg, Action<string> target)
        {
            return () => target(msg);
        }

        public static Action CreateGeneric<T>(T msg, Action<T> target)
        {
            return () => target(msg);
        }

        [Test]
        public void PerfTestWithInt()
        {
            void OnMsg(int x)
            {
            }

            var fact = new ActionFactory<int>(OnMsg);
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 5000000; i++)
                fact.Create(1);
            watch.Stop();
            Console.WriteLine("Elapsed: " + watch.ElapsedMilliseconds);
        }

        [Test]
        public void PerfTestWithObjectString()
        {
            void OnMsg(string x)
            {
                if (x == "end")
                    Console.WriteLine(x);
            }

            var fact = new ActionFactory<string>(OnMsg);
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 5000000; i++)
            {
                Action act = fact.CreateObject("s");
                act();
            }
            fact.Create("end")();
            watch.Stop();
            Console.WriteLine("Elapsed: " + watch.ElapsedMilliseconds);
        }

        [Test]
        public void PerfTestWithString()
        {
            void OnMsg(string x)
            {
                if (x == "end")
                    Console.WriteLine(x);
            }

            var fact = new ActionFactory<string>(OnMsg);
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 5000000; i++)
            {
                Action act = fact.Create("s");
                act();
            }
            fact.Create("end")();
            watch.Stop();
            Console.WriteLine("Elapsed: " + watch.ElapsedMilliseconds);
        }

        [Test]
        public void PerfTestWithStringGenericStaticInline()
        {
            void OnMsg(string x)
            {
            }

            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 5000000; i++)
            {
                Action act = CreateGeneric("", OnMsg);
                act();
            }
            watch.Stop();
            Console.WriteLine("Elapsed: " + watch.ElapsedMilliseconds);
        }

        [Test]
        public void PerfTestWithStringInline()
        {
            void OnMsg(string x)
            {
                if (x == "end")
                    Console.WriteLine(x);
            }

            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 5000000; i++)
            {
                void Act() => OnMsg(i.ToString());
                Act();
            }
            void End() => OnMsg("end");
            End();
            watch.Stop();
            Console.WriteLine("Elapsed: " + watch.ElapsedMilliseconds);
        }

        [Test]
        public void PerfTestWithStringStaticInline()
        {
            void OnMsg(string x)
            {
            }

            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 5000000; i++)
            {
                Action act = CreateString("", OnMsg);
                act();
            }
            watch.Stop();
            Console.WriteLine("Elapsed: " + watch.ElapsedMilliseconds);
        }
    }
}