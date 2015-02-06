using System;
using Xunit;
using CodeConnect.MemoryMappedQueue;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    public class DataStructureTests
    {
        [Theory]
        [InlineData(8)]
        [InlineData(1 * 1024)]
        [InlineData(20 * 1024)]
        [InlineData(5 * 1024 * 1024)]
        public void InitializeTest(int initialSize)
        {
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, "test"))
            {
                using (var readQueue = new MemoryMappedQueue(initialSize, false, "test"))
                {
                    var test = readQueue.GetHashCode();
                }
            }
        }

        [Fact]
        public void InitializeTooLargeTest()
        {
            var initialSize = int.MaxValue;
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, true, "test"));
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, false, "test"));
        }

        [Theory]
        [InlineData(7)]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(- 5 * 1024 * 1024)]
        [InlineData(int.MinValue)]
        public void InitializeTooSmallTest(int initialSize)
        {
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, true, "test"));
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, false, "test"));
        }
    }
}

