using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace CodeConnect.MemoryMappedQueue
{
    public class InterprocessDequeuer
    {
        private readonly MemoryMappedQueue _mmq;
        private BlockingCollection<Object> _sourceQueue;
        private Task _dataRetriever;
        private CancellationTokenSource _tokenSource;
        private BinaryFormatter _bf;

        public InterprocessDequeuer(int size, string name)
        {
            _mmq = new MemoryMappedQueue(size, false, name);
            _sourceQueue = new BlockingCollection<object>();
            _bf = new BinaryFormatter();
            _tokenSource = new CancellationTokenSource();
            var taskCancellationToken = _tokenSource.Token;

            /*_dataRetriever = new Task(() => retrieveData(taskCancellationToken), taskCancellationToken);*/
        }

        public object Dequeue()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] receivedData = _mmq.Dequeue();
                ms.Write(receivedData, 0, receivedData.Length);
                ms.Seek(0, SeekOrigin.Begin);
                return (Object)_bf.Deserialize(ms);
            }
        }


        // TODO:
        // Expose the callback for a method that automatically
        // dequeues whenever there is data
    }
}
