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
        public void MultipleDataTransfersSlowReceiver()
        {
            var initialSize = 24;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    // Note that each enqueue operation writes 8 bytes (4 for data and 4 for its length)
                    byte[] testData = { 1, 2, 3, 4 }; // 4 + 4
                    writeQueue.Enqueue(testData); // 8 bytes filled

                    byte[] testData2 = { 5, 6, 7, 8 }; // 4 + 4
                    writeQueue.Enqueue(testData2); // 16 bytes filled

                    byte[] testData3 = { 9, 10, 11, 12 }; // 4 + 4
                    writeQueue.Enqueue(testData3); // 24 bytes filled

                    var receivedData = readQueue.Dequeue();
                    Assert.Equal(testData, receivedData);
                    var receivedData2 = readQueue.Dequeue();
                    Assert.Equal(testData2, receivedData2);
                    var receivedData3 = readQueue.Dequeue();
                    Assert.Equal(testData3, receivedData3);
                }
            }
        }

        [Fact]
        public void MultipleDataTransfersOutOfMemory()
        {
            var initialSize = 24;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = { 1, 2, 3, 4 }; // 4 + 4
                    writeQueue.Enqueue(testData); // 8 bytes filled

                    byte[] testData2 = { 5, 6, 7, 8 }; // 4 + 4
                    writeQueue.Enqueue(testData2); // 16 bytes filled

                    byte[] testData3 = { 9, 10, 11, 12, 0 }; // 5 + 4
                    Assert.Throws(typeof(OutOfMemoryException), () => writeQueue.Enqueue(testData3)); // 25 bytes filled
                }
            }
        }

        [Fact]
        public void MultipleDataTransfersWithOverflow()
        {
            var initialSize = 32;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = { 1, 2, 3, 4, 5, 6, 7, 8 }; // 8 + 4
                    writeQueue.Enqueue(testData); // 12 B filled
                    var receivedData = readQueue.Dequeue();
                    Assert.Equal(testData, receivedData);

                    byte[] testData2 = { 9, 10, 11, 12, 13, 14, 15, 16 }; // 8 + 4
                    writeQueue.Enqueue(testData2); // 24 B filled
                    var receivedData2 = readQueue.Dequeue();
                    Assert.Equal(testData2, receivedData2);

                    byte[] testData3 = { 17, 18, 19, 20 }; // 4 + 4
                    writeQueue.Enqueue(testData3); // 32 B filled
                    var receivedData3 = readQueue.Dequeue();
                    Assert.Equal(testData3, receivedData3);

                    byte[] testData4 = { 21, 22, 23, 24 }; // 4 + 4
                    writeQueue.Enqueue(testData4); // 40 B filled
                    var receivedData4 = readQueue.Dequeue();
                    Assert.Equal(testData4, receivedData4);
                }
            }
        }

        [Fact]
        public void MultipleDataTransfersWithTrickyOverflow()
        {
            var initialSize = 30;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = { 1, 2, 3, 4, 5, 6, 7, 8 }; // 8 + 4
                    writeQueue.Enqueue(testData); // 12 B filled
                    var receivedData = readQueue.Dequeue();
                    Assert.Equal(testData, receivedData);

                    byte[] testData2 = { 9, 10, 11, 12, 13, 14, 15, 16 }; // 8 + 4
                    writeQueue.Enqueue(testData2); // 24 B filled
                    var receivedData2 = readQueue.Dequeue();
                    Assert.Equal(testData2, receivedData2);

                    byte[] testData3 = { 17, 18, 19, 20, 21, 22 }; // 6 + 4
                    writeQueue.Enqueue(testData3); // 34 B filled
                    var receivedData3 = readQueue.Dequeue();
                    Assert.Equal(testData3, receivedData3);
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
                    byte[] testData = new byte[10]; // 10 + 4
                    writeQueue.Enqueue(testData);
                    Assert.Throws(typeof(OutOfMemoryException), () => writeQueue.Enqueue(testData));
                }
            }
        }
        
        [Fact]
        public void TooMuchAtOnce()
        {
            var initialSize = 16; // dataSize of 12 will hit the limit
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {

                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = new byte[13]; // dataSize + 4
                    Assert.Throws(typeof(ArgumentOutOfRangeException), () => writeQueue.Enqueue(testData));
                }
            }
        }
    }
}
