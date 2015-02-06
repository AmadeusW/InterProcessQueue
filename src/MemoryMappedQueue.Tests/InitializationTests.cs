using System;
using Xunit;
using CodeConnect.MemoryMappedQueue;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    [Collection("Data structure initialization")]
    public class InitializationTests
    {
        [Theory]
        [InlineData(16)] // 16 B
        [InlineData(1 * 1024)] // 1 KB
        [InlineData(20 * 1024)] // 20 KB
        [InlineData(5 * 1024 * 1024)] // 5 MB
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
        [InlineData(15)]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(- 5 * 1024 * 1024)]
        [InlineData(int.MinValue)]
        public void InitializeTooSmallTest(int initialSize)
        {
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, true, "test"));
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize, false, "test"));
        }

        [Fact]
        public void OperationPermissionTest()
        {
            var initialSize = 50;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, "test"))
            {
                Assert.Throws(typeof(InvalidOperationException), () => writeQueue.Dequeue());
                using (var readQueue = new MemoryMappedQueue(initialSize, false, "test"))
                {
                    Assert.Throws(typeof(InvalidOperationException), () => readQueue.Enqueue(null));
                }
            }
        }
    }
}

