using System;
using Xunit;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    [Collection("Operation tests")]
    public class OperationTests : MemoryMappedQueueTests
    {
        [Fact]
        public void SimpleDataTransfer()
        {
            var initialSize = 50;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {
                
                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = new byte[3];
                    writeQueue.Enqueue(testData);
                    var receivedData = readQueue.Dequeue();

                    Assert.Equal(testData, receivedData);
                }
            }
        }

        [Fact]
        public void OutOfMemory()
        {
            var initialSize = 16;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = new byte[10];
                    writeQueue.Enqueue(testData);
                    Assert.Throws(typeof(OutOfMemoryException), () => writeQueue.Enqueue(testData));
                }
            }
        }
        
        [Fact]
        public void TooMuchAtOnce()
        {
            var initialSize = 16;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = new byte[17];
                    Assert.Throws(typeof(ArgumentOutOfRangeException), () => writeQueue.Enqueue(testData));
                }
            }
        }
    }
}
