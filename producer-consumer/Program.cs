using System;
using System.Collections.Generic;
using System.Threading;

namespace producer_consumer
{
    public class TaskQueue<T>
    {
        private Mutex queueLock;
        private volatile List<T> queue;
        private CancellationTokenSource cancelTokenSource;

        public TaskQueue()
        {
            queueLock = new Mutex();
            queue = new List<T>();
            cancelTokenSource = new CancellationTokenSource();
        }

        public CancellationToken GetToken()
        {
            return cancelTokenSource.Token;
        }

        public void Shutdown()
        {
            cancelTokenSource.Cancel();
        }

        public int size()
        {
            return queue.Count;
        }

        public bool IsEmpty()
        {
            return queue.Count == 0;
        }

        public void Put_Task(T item)
        {
            queueLock.WaitOne();
            try
            {
                queue.Add(item);
            }
            finally
            {
                queueLock.ReleaseMutex();
            }
        }

        public T Get_Task()
        {
            if (IsEmpty())
            {
                throw new Exception();
            }

            queueLock.WaitOne();
            T task;
            try
            {
                task = queue[0];
                queue.RemoveAt(0);
            }
            finally
            {
                queueLock.ReleaseMutex();
            }

            return task;
        }
    }

    public abstract class ThreadAction<T>
    {
        protected Thread _thread;
        protected volatile TaskQueue<T> _TaskQueue;
        protected T _store;
        protected bool _cooldown;
        protected CancellationToken _token;

        public ThreadAction(TaskQueue<T> tq)
        {  
            _TaskQueue = tq;
            _thread = new Thread(new ThreadStart(Action));
            _thread.Start();
            _token = tq.GetToken();
            _cooldown = false;
        }

        protected abstract void Action();
    }

    public class Consumer<T> : ThreadAction<T>
    {
        public Consumer(TaskQueue<T> tq) : base(tq) { }

        override
        protected void Action()
        {
            while (!_token.IsCancellationRequested)
            {
                if (!_cooldown || _TaskQueue.IsEmpty())
                {
                    try
                    {
                        _store = _TaskQueue.Get_Task();
                        _cooldown = true;
                        Console.WriteLine(_store);
                    }
                    catch (Exception e) { }
                }
                else
                {
                    Thread.Sleep(2000);
                    _cooldown = false;
                }
            }
        }
    }

    public class Producer<T> : ThreadAction<T>
    {
        private Func<T> _produce;

        public Producer(TaskQueue<T> tq) : this(tq, ()=> { return default(T); }) { }

        public Producer(TaskQueue<T> tq, Func<T> func) : base(tq)
        {
            _produce = func;
        }

        override
        protected void Action()
        {
            while (!_token.IsCancellationRequested)
            {
                if (!_cooldown)
                {
                    _store = _produce();
                    _cooldown = true;
                    _TaskQueue.Put_Task(_store);
                }
                else
                {
                    Thread.Sleep(2000);
                    _cooldown = false;
                }
            }
        }
    }

    class Program
    {
        const int pCount = 3, cCount = 5;

        static string func ()
        {
            return String.Format("Created by {0}", Thread.GetCurrentProcessorId());
        }

        static void Main(string[] args)
        {
            TaskQueue<string> taskQueue = new TaskQueue<string>();

            Console.WriteLine("Starting producers...");
            List<Producer<string>> producers = new List<Producer<string>>();
            for (int i = 0; i < pCount; ++i)
            {
                producers.Add(new Producer<string>(taskQueue, func));
            }

            Console.WriteLine("Starting consumers...");
            List<Consumer<string>> consumers = new List<Consumer<string>>();
            for (int i = 0; i < cCount; ++i)
            {
                consumers.Add(new Consumer<string>(taskQueue));
            }

            /*while (true)
            {
                Console.WriteLine(taskQueue.size());
                Thread.Sleep(1000);
            }*/

            Console.ReadKey();

            taskQueue.Shutdown();
        }
    }
}
