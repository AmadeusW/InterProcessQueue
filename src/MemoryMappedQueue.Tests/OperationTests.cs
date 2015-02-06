using System;
using Xunit;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    [Collection("Data structure operation")]
    public class OperationTests
    {
        [Fact]
        public void SimpleDataTransfer()
        {
            var initialSize = 50;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, "test"))
            {
                
                using (var readQueue = new MemoryMappedQueue(initialSize, false, "test"))
                {
                    byte[] testData = { 1, 2, 3 };
                    writeQueue.Enqueue(testData);
                    var receivedData = readQueue.Dequeue();

                    Assert.Equal(testData, receivedData);
                }
            }
        }

        [Fact]
        public void OverflowTest()
        {
            var initialSize = 10;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, "test"))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, "test"))
                {
                    byte[] testData = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                    Assert.Throws(typeof(OutOfMemoryException), () => writeQueue.Enqueue(testData));
                }
            }
        }
    }
}
