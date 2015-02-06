using System;

namespace CodeConnect.MemoryMappedQueue
{
    public class MemoryMappedQueue
    {
        private uint _readPointer, _writePointer;
        private readonly int _sizeOfPointer;
        private byte[] _data;

        /// <summary>
        /// Creates a circular array that occupies specified amount of space
        /// </summary>
        /// <param name="size">Total size of this data structure</param>
        public MemoryMappedQueue(int size)
        {
            _readPointer = 0;
            _writePointer = 0;
            _sizeOfPointer = sizeof(int);

            // Ensure specified size is within bounds (2GB)
            if (size >= int.MaxValue)
            {
                throw new ArgumentException("Addressable size is too large.", nameof(size));
            }
            // Ensure that there is enough room for at least one piece of data
            else if (size < 4 * _sizeOfPointer)
            {
                throw new ArgumentException("Specified size is too small", nameof(size));
            }

            var dataSize = size - 2 * _sizeOfPointer;
            _data = new byte[dataSize];
        }
    }
}
