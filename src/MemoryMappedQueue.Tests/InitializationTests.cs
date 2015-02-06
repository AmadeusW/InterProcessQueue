using System;
using Xunit;
using CodeConnect.MemoryMappedQueue;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    [Collection("Initialization tests")]
    public class InitializationTests : MemoryMappedQueueTests
    {
        [Theory]
        [InlineData(16)] // 16 B
        [InlineData(1 * 1024)] // 1 KB
        [InlineData(20 * 1024)] // 20 KB
        [InlineData(5 * 1024 * 1024)] // 5 MB
        public void InitializeTest(int initialSize)
        {
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {
                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    var test = readQueue.GetHashCode();
                }
            }
        }

        [Fact]
        public void InitializeTooLargeTest()
        {
            var initialSize = int.MaxValue;
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, true, AccessName));
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, false, AccessName));
        }

        [Theory]
        [InlineData(15)]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(- 5 * 1024 * 1024)]
        [InlineData(int.MinValue)]
        public void InitializeTooSmallTest(int initialSize)
        {
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, true, AccessName));
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, false, AccessName));
        }

        [Fact]
        public void OperationPermissionTest()
        {
            var initialSize = 50;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {
                Assert.Throws(typeof(InvalidOperationException), () => writeQueue.Dequeue());
                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    Assert.Throws(typeof(InvalidOperationException), () => readQueue.Enqueue(null));
                }
            }
        }
    }
}

