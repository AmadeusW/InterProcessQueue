using System;
using Xunit;
using CodeConnect.MemoryMappedQueue;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    public class DataStructureTests
    {
        [Theory]
        [InlineData(16)]
        [InlineData(1 * 1024)]
        [InlineData(20 * 1024)]
        [InlineData(5 * 1024 * 1024)]
        public void InitializeTest(int initialSize)
        {
            var queue = new MemoryMappedQueue(initialSize);
        }

        [Fact]
        public void InitializeTooLargeTest()
        {
            var initialSize = int.MaxValue;
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize));
        }

        [Theory]
        [InlineData(15)]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(- 5 * 1024 * 1024)]
        [InlineData(int.MinValue)]
        public void InitializeTooSmallTest(int initialSize)
        {
            Assert.Throws(typeof(ArgumentException), () => new MemoryMappedQueue(initialSize));
        }
    }
}

