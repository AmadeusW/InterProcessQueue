using System;
using System.IO.MemoryMappedFiles;

namespace CodeConnect.MemoryMappedQueue
{
    public class MemoryMappedQueue : IDisposable
    {
        private uint _readPointer, _writePointer;
        private byte[] _data;
        private MemoryMappedFile _dataFile;
        private MemoryMappedFile _pointersFile;
        private bool _writer;

        /// <summary>
        /// Creates a circular array that occupies specified amount of space
        /// </summary>
        /// <param name="size">Total size of this data structure</param>
        public MemoryMappedQueue(int size, bool writer, string name)
        {
            _readPointer = 0;
            _writePointer = 0;
            _writer = writer;

            int sizeOfPointers = 2 * sizeof(int);
            string dataFileName = name + "_data";
            string pointersFileName = name + "_pointers";

            // Ensure specified size is within bounds (2GB)
            if (size >= int.MaxValue)
            {
                throw new ArgumentException("Addressable size is too large.", nameof(size));
            }
            // Ensure that there is enough room for at least one piece of data
            else if (size < sizeOfPointers)
            {
                throw new ArgumentException("Specified size is too small", nameof(size));
            }

            _data = new byte[size];

            // Access the memory mapped file
            if (writer)
            {
                _dataFile = MemoryMappedFile.CreateNew(dataFileName, size, MemoryMappedFileAccess.ReadWrite);
                _pointersFile = MemoryMappedFile.CreateNew(pointersFileName, sizeOfPointers, MemoryMappedFileAccess.ReadWrite);
            }
            else
            {
                _dataFile = MemoryMappedFile.OpenExisting(dataFileName, MemoryMappedFileRights.Read);
                _pointersFile = MemoryMappedFile.OpenExisting(pointersFileName, MemoryMappedFileRights.Read);
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
