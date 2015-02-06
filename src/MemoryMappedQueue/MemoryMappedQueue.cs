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
        private long _readPointer, _writePointer, _overflowWritePointer;
        private bool _usingOverflowWritePointer;

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
            _overflowWritePointer = 0;
            _usingOverflowWritePointer = false;
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
                _pointersFile = MemoryMappedFile.CreateNew(pointersFileName, 3 * _pointerSize, MemoryMappedFileAccess.ReadWrite);
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
                loadReadPointer(); // This may update the write location!
                var writeLocation = getWriteLocation(grossDataLength);

                using (MemoryMappedViewAccessor dataAccessor = _dataFile.CreateViewAccessor(_writePointer, grossDataLength))
                {
                    // Writes [ dataLength | data ................................ ]
                    dataAccessor.Write<Int32>(0, ref dataLength);
                    dataAccessor.WriteArray<byte>(sizeof(Int32), serializedData, 0, dataLength);
                }

                // Update the pointers
                if (_usingOverflowWritePointer)
                {
                    _overflowWritePointer = writeLocation + grossDataLength;
                }
                else
                {
                    _writePointer = writeLocation + grossDataLength;
                }
                storeWritePointer();
            }
            finally
            {
                _key.ReleaseMutex();
            }
        }

        private long getWriteLocation(int grossDataLength)
        {
            if (_usingOverflowWritePointer)
            {
                if (canUseSpace(_overflowWritePointer, _readPointer, grossDataLength))
                {
                    return _overflowWritePointer;
                }
                else
                {
                    // w::::R++++W- Data will overflow past the read pointer R
                    string exceptionMessage = "There is not enough space for this data. ";
#if DEBUG
                    exceptionMessage += Diagnostics();
#endif
                    throw new OutOfMemoryException(exceptionMessage);
                }
            }
            else
            {
                if (canUseSpace(_writePointer, _dataSize, grossDataLength))
                {
                    return _writePointer;
                }
                else
                {
                    // Reset the overflow pointer and attempt to use it
                    _usingOverflowWritePointer = true;
                    _overflowWritePointer = 0;
                    return getWriteLocation(grossDataLength);
                }
            }
        }

        private bool canUseSpace(long startPosition, long endPosition, int dataLength)
        {
            return startPosition + dataLength <= endPosition;
        }

        private long getReadLocation()
        {
            if (_readPointer == _writePointer)
            {
                // We've reached the write pointer.
                // This mean either that there is an overflow:
                if (_usingOverflowWritePointer)
                {
                    // ++++w----RW-
                    return 0;
                }
                // Or that there is nothing to read
                else
                {
                    // ------RW---
                    return -1;
                }
            }
            else if (_readPointer < _writePointer)
            {
                return _readPointer;
            }
            else
            {
                string exceptionMessage = "The data structure is in a corrupted state.";
#if DEBUG
                exceptionMessage += Diagnostics();
#endif
                throw new ApplicationException(exceptionMessage);
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
                // First piece of data (sizeof(Int32)) contains length of actual data chunk
                using (MemoryMappedViewAccessor dataAccessor = _dataFile.CreateViewAccessor(_readPointer, sizeof(Int32), MemoryMappedFileAccess.Read))
                {
                    dataLength = dataAccessor.ReadInt32(0);
                }
                byte[] data = new byte[dataLength];
                Int32 grossDataLength = dataLength + sizeof(Int32);

                // Update the write pointers, so that we know if there is an overflow
                loadWritePointer();
                var readLocation = getReadLocation();
                if (readLocation == -1)
                {
                    // There is nothing to read
                    return null;
                }
                
                using (MemoryMappedViewAccessor dataAccessor = _dataFile.CreateViewAccessor(_readPointer, grossDataLength, MemoryMappedFileAccess.Read))
                {
                    dataAccessor.ReadArray<byte>(sizeof(Int32), data, 0, dataLength);
                }

                _readPointer = readLocation + grossDataLength;
                storeReadPointer();

                return data;
            }
            finally
            {
                _key.ReleaseMutex();
            }
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
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 3 * _pointerSize))
            {
                pointerAccessor.Write(_pointerSize, _writePointer);
                pointerAccessor.Write(_pointerSize * 2, _overflowWritePointer);
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
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 3 * _pointerSize))
            {
                pointerAccessor.Write(0, _readPointer);
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
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 3 * _pointerSize))
            {
                long newWritePointer, newOverflowWritePointer;
                pointerAccessor.Read<long>(_pointerSize, out newWritePointer);
                pointerAccessor.Read<long>(_pointerSize * 2, out newOverflowWritePointer);
                _writePointer = newWritePointer;
                _overflowWritePointer = newWritePointer;
                _usingOverflowWritePointer = _overflowWritePointer > -1;
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
            using (MemoryMappedViewAccessor pointerAccessor = _pointersFile.CreateViewAccessor(0, 3 * _pointerSize))
            {
                long newReadPointer;
                pointerAccessor.Read<long>(0, out newReadPointer);

                // If the new read pointer is less than the current read pointer,
                // it means that the reader read everything from standard buffer
                // and is now reading from the overflow buffer
                if (newReadPointer < _readPointer)
                {
                    _writePointer = _overflowWritePointer;
                    _usingOverflowWritePointer = false;
                    _overflowWritePointer = -1;
                }

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

        public string Diagnostics()
        {
            int diagnosticSize = 50;
            int extraCharacterNumber = 3; // w,W and R
            char[] diagnostic = new char[diagnosticSize];

            double overflowData = 0;
            if (_usingOverflowWritePointer)
            {
                overflowData = Math.Round(_overflowWritePointer / (double)_dataSize * (diagnosticSize - extraCharacterNumber));
            }
            var initialEmpty = Math.Round((_readPointer - _overflowWritePointer) / (double)_dataSize * (diagnosticSize - extraCharacterNumber));
            var data = Math.Round((_writePointer - _readPointer) / (double)_dataSize * (diagnosticSize - extraCharacterNumber));
            var finalEmpty = Math.Round((_dataSize - _writePointer) / (double)_dataSize * (diagnosticSize - extraCharacterNumber));

            int charactersDrawn = 0;

            for (int i = charactersDrawn; i < overflowData; i++)
            {
                diagnostic[charactersDrawn] = '+'; // Overflow data
            }
            diagnostic[charactersDrawn++] = 'w'; // Overflow write pointer
            for (int i = 0; i < initialEmpty; i++, charactersDrawn++)
            {
                diagnostic[charactersDrawn] = '-'; // Unused space
            }
            diagnostic[charactersDrawn++] = 'R'; // Read pointer
            for (int i = 0; i < data; i++, charactersDrawn++)
            {
                diagnostic[charactersDrawn] = '+'; // Data
            }
            diagnostic[charactersDrawn++] = 'W'; // Write pointer
            for (int i = charactersDrawn; i < diagnosticSize; i++)
            {
                diagnostic[i] = '-'; // Unused space
            }
            string details = String.Format(" {4}, size: {0}, R: {1}, W: {2}, w: {3}", _dataSize, _readPointer, _writePointer, _overflowWritePointer, _writer ? "Writer" : "Reader" );
            return new string(diagnostic) + details;
        }

    }
}
