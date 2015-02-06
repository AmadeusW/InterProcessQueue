using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace CodeConnect.MemoryMappedQueue
{
    public class MemoryMappedQueue : IDisposable
    {
        // Main memory mapped file. Stores data
        private readonly MemoryMappedFile _dataFile;
        private readonly int _dataSize;
        private byte[] _data;

        // Helper memory mapped file. Shares positions of read and write pointers
        private readonly MemoryMappedFile _pointersFile;
        private readonly int _pointerSize;
        private long _readPointer, _writePointer;

        private readonly bool _writer;
        private readonly Mutex _key;
        private const int MUTEX_TIMEOUT = 1000;

        /// <summary>
        /// Creates a circular array that occupies specified amount of space
        /// </summary>
        /// <param name="size">Total size of this data structure</param>
        public MemoryMappedQueue(int size, bool writer, string name)
        {
            _readPointer = 0;
            _writePointer = 0;
            _writer = writer;
            _pointerSize = sizeof(long);
            _key = new Mutex(initiallyOwned: _writer, name: name);

            // Ensure specified size is within bounds (2GB)
            if (size >= int.MaxValue)
            {
                throw new ArgumentException("Addressable size is too large.", nameof(size));
            }
            // Ensure that there is enough room for a little data
            else if (size < 2 * _pointerSize)
            {
                throw new ArgumentException("Specified size is too small", nameof(size));
            }

            _dataSize = size;
            _data = new byte[_dataSize];
            string dataFileName = name + "_data";
            string pointersFileName = name + "_pointers";

            // Access the memory mapped file
            if (writer)
            {
                _dataFile = MemoryMappedFile.CreateNew(dataFileName, _dataSize, MemoryMappedFileAccess.ReadWrite);
                _pointersFile = MemoryMappedFile.CreateNew(pointersFileName, 2 * _pointerSize, MemoryMappedFileAccess.ReadWrite);
            }
            else
            {
                _dataFile = MemoryMappedFile.OpenExisting(dataFileName, MemoryMappedFileRights.Read);
                _pointersFile = MemoryMappedFile.OpenExisting(pointersFileName, MemoryMappedFileRights.ReadWrite);
            }
        }

        /// <summary>
        /// Writes provided data to the data structure and makes it available to the corresponding reading queue.
        /// </summary>
        /// <param name="serializedData">Data to write</param>
        /// <returns>true if write was successful</returns>
        public void Enqueue(byte[] serializedData)
        {
            if (!_writer)
            {
                throw new InvalidOperationException("This MemoryMappedQueue can only dequeue. Set writer=true in the constructor for enqueuing.");
            }
            Int32 dataLength = serializedData.Length;
            Int32 grossDataLength = dataLength + sizeof(Int32);

            _key.WaitOne(MUTEX_TIMEOUT);
            try
            {
                loadReadPointer();
                bool overflowFlag;
                var newWritePointer = advancePointer(_writePointer, grossDataLength, _readPointer, out overflowFlag);
                if (overflowFlag)
                {
                    throw new OutOfMemoryException("The queue is too full to enque this data. ");
                }

                using (MemoryMappedViewAccessor dataAccessor = _dataFile.CreateViewAccessor(_writePointer, grossDataLength))
                {
                    // Writes [ dataLength | data ................................ ]
                    dataAccessor.Write<Int32>(0, ref dataLength);
                    dataAccessor.WriteArray<byte>(sizeof(Int32), serializedData, 0, dataLength);
                }
                _writePointer = newWritePointer;
                storeWritePointer();
            }
            finally
            {
                _key.ReleaseMutex();
            }
        }

        /// <summary>
        /// Reads next available piece of data.
        /// </summary>
        /// <returns>Piece of data read from the queue</returns>
        public byte[] Dequeue()
        {
            if (_writer)
            {
                throw new InvalidOperationException("This MemoryMappedQueue can only enqueue. Set writer=false in the constructor for dequeuing.");
            }

            _key.WaitOne(MUTEX_TIMEOUT);
            try
            {
                Int32 dataLength;
                using (MemoryMappedViewAccessor dataAccessor = _dataFile.CreateViewAccessor(_readPointer, sizeof(Int32), MemoryMappedFileAccess.Read))
                {
                    dataLength = dataAccessor.ReadInt32(0);
                }
                byte[] data = new byte[dataLength];
                bool overflowFlag;
                Int32 grossDataLength = dataLength + sizeof(Int32);
                long newReadPointer = advancePointer(_readPointer, grossDataLength, _writePointer, out overflowFlag);
                if (overflowFlag)
                {
                    throw new InvalidOperationException("There is no more data in the queue");
                }

                using (MemoryMappedViewAccessor dataAccessor = _dataFile.CreateViewAccessor(_readPointer, grossDataLength, MemoryMappedFileAccess.Read))
                {
                    dataAccessor.ReadArray<byte>(sizeof(Int32), data, 0, dataLength);
                }

                _readPointer = newReadPointer;
                storeReadPointer();

                return data;
            }
            finally
            {
                _key.ReleaseMutex();
            }
        }

        /// <summary>
        /// Advances the pointer and overflows it around the data structure's size
        /// </summary>
        /// <param name="pointer">Pointer to increment</param>
        /// <param name="increment">Increment amount</param>
        /// <returns></returns>
        private long advancePointer(long pointer, int increment, long overflowCheck, out bool overflowFlag)
        {
            if (increment <= 0 || increment >= _dataSize)
            {
                throw new ArgumentOutOfRangeException(nameof(increment));
            }

            bool initialRelation = pointer >= overflowCheck;
            bool finalRelation;

            pointer += increment;
            if (pointer > _dataSize)
            {
                pointer -= _dataSize;
                // If despite overflow we are *again* ahead of other pointer, it means we're out of memory
                finalRelation = pointer >= overflowCheck;
                overflowFlag = initialRelation == finalRelation;
            }
            else
            {
                // There was no overflow. If we swapped spots with the other pointer, it means we're out of memory
                finalRelation = pointer >= overflowCheck;
                overflowFlag = initialRelation != finalRelation;
            }

            return pointer;
        }

        /// <summary>
        /// Stores current value of _writePointer in shared memory.
        /// Must be called from a synchronized context!
        /// </summary>
        private void storeWritePointer()
        {
            if (!_writer)
            {
                throw new InvalidOperationException("This MemoryMappedQueue can only move the read pointer. Set writer=true in the constructor for enqueuing.");
            }
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 2 * _pointerSize))
            {
                pointerAccessor.Write(0, _writePointer);
            }
        }

        /// <summary>
        /// Stores current value of _readPointer in shared memory.
        /// Must be called from a synchronized context!
        /// </summary>
        private void storeReadPointer()
        {
            if (_writer)
            {
                throw new InvalidOperationException("This MemoryMappedQueue can only move the write pointer. Set writer=false in the constructor for dequeuing.");
            }
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 2 * _pointerSize))
            {
                pointerAccessor.Write(_pointerSize, _readPointer);
            }
        }

        /// <summary>
        /// Gets current value of _writePointer from shared memory.
        /// Must be called from a synchronized context!
        /// </summary>
        private void loadWritePointer()
        {
            if (_writer)
            {
                throw new InvalidOperationException("Writing MemoryMappedQueue must not read its property (write pointer) from the memory mapped file.");
            }
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 2 * _pointerSize))
            {
                long newWritePointer;
                pointerAccessor.Read<long>(0, out newWritePointer);
                _writePointer = newWritePointer;
            }
        }

        /// <summary>
        /// Gets current value of _readPointer in shared memory.
        /// Must be called from a synchronized context!
        /// </summary>
        private void loadReadPointer()
        {
            if (!_writer)
            {
                throw new InvalidOperationException("Reading MemoryMappedQueue must not read its property (read pointer) from the memory mapped file.");
            }
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 2 * _pointerSize))
            {
                long newReadPointer;
                pointerAccessor.Read<long>(_pointerSize, out newReadPointer);
                _readPointer = newReadPointer;
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
                    _dataFile?.Dispose();
                    _pointersFile?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                _data = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources. 
        // ~MemoryMappedQueue() {
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
