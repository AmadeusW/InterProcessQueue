using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace CodeConnect.MemoryMappedQueue
{
    public class InterprocessEnqueuer : IDisposable
    {
        private readonly MemoryMappedQueue _mmq;
        private BlockingCollection<Object> _sinkQueue;
        private Task _dataProcessor;
        private CancellationTokenSource _tokenSource;
        private BinaryFormatter _bf;
        private bool _ignoreIncorrectItems;
        private int _maxDataSize;

        public InterprocessEnqueuer(int size, string name, int maxDataSize = 1024 * 1024, bool ignoreIncorrectItems = true)
        {
            _mmq = new MemoryMappedQueue(size, true, name);
            _sinkQueue = new BlockingCollection<object>(); // By default, it is a ConcurrentQueue
            _tokenSource = new CancellationTokenSource();
            _bf = new BinaryFormatter();
            var taskCancellationToken = _tokenSource.Token;
            _maxDataSize = maxDataSize;
            _ignoreIncorrectItems = ignoreIncorrectItems;

            _dataProcessor = new Task(() => processData(taskCancellationToken), taskCancellationToken);
        }

        public void Enqueue(object item)
        {
            _sinkQueue.Add(item);
        }

        private void processData(CancellationToken token)
        {
            // This is a blocking call:
            foreach (var item in _sinkQueue.GetConsumingEnumerable(token))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    _bf.Serialize(ms, item);
                    var serializedData = ms.ToArray();
                    if (serializedData.Length > _maxDataSize)
                    {
                        if (!_ignoreIncorrectItems)
                        {
                            throw new ArgumentException(String.Format("Enqueued item is too large. It is {0} B and  the limit is {1} B.", serializedData.Length, _maxDataSize));
                        }
                        continue;
                    }
                    try
                    {
                        _mmq.Enqueue(serializedData);
                    }
                    catch (ArgumentNullException ex)
                    {
                        if (!_ignoreIncorrectItems)
                        {
                            throw;
                        }
                    }
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _mmq.Dispose();
                    _tokenSource.Cancel();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                _bf = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources. 
        // ~MemoryDataSink() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
