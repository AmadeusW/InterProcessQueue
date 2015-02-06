using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    [Collection("Advanced operation tests")]
    public class AdvancedOperationTests : MemoryMappedQueueTests
    {
        [Theory]
        [InlineData("Sample Text")]
        [InlineData(" ")]
        [InlineData("Complex characters ѭѯϠṨἯ⅚♪ˠȺż")]
        public void PrimitiveStringTransfer(string input)
        {
            var initialSize = 1000;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {
                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = new byte[input.Length * sizeof(char)];
                    Buffer.BlockCopy(input.ToCharArray(), 0, testData, 0, testData.Length);

                    writeQueue.Enqueue(testData);
                    var receivedData = readQueue.Dequeue();

                    var receivedCharacters = new char[receivedData.Length / sizeof(char)];
                    Buffer.BlockCopy(receivedData, 0, receivedCharacters, 0, receivedData.Length);
                    var receivedString = new string(receivedCharacters);
                    Assert.Equal(input, receivedString);
                }
            }
        }

        [Theory]
        [InlineData("Sample Text")]
        [InlineData(" ")]
        [InlineData("Complex characters ѭѯϠṨἯ⅚♪ˠȺż")]
        public void EncodedStringTransfer(string input)
        {
            var initialSize = 1000;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {
                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = System.Text.Encoding.Unicode.GetBytes(input);
                    writeQueue.Enqueue(testData);
                    var receivedData = readQueue.Dequeue();
                    var receivedString = System.Text.Encoding.Unicode.GetString(receivedData);
                    Assert.Equal(input, receivedString);
                }
            }
        }

        [Theory]
        [InlineData("")]
        public void QueueRefusesEmptyString(string input)
        {
            var initialSize = 1000;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {
                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    byte[] testData = new byte[input.Length * sizeof(char)];
                    Buffer.BlockCopy(input.ToCharArray(), 0, testData, 0, testData.Length);

                    Assert.Throws(typeof(ArgumentNullException), () => writeQueue.Enqueue(testData));
                }
            }
        }

        [Fact]
        public void StructTransfer()
        {
            System.Diagnostics.Debug.WriteLine("StructTransfer");
            var initialSize = 1000;
            using (var writeQueue = new MemoryMappedQueue(initialSize, true, AccessName))
            {
                using (var readQueue = new MemoryMappedQueue(initialSize, false, AccessName))
                {
                    List <double> numbers = new List<double>();
                    List <double> receivedNumbers;
                    for (int i = 0; i < 5; i++)
                    {
                        numbers.Add(Math.PI * i);
                    }

                    byte[] testData; ;

                    BinaryFormatter bf = new BinaryFormatter();
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bf.Serialize(ms, numbers);
                        testData = ms.ToArray();
                    }

                    writeQueue.Enqueue(testData);
                    var receivedData = readQueue.Dequeue();

                    System.Diagnostics.Debug.WriteLine(writeQueue.Diagnostics());
                    System.Diagnostics.Debug.WriteLine(readQueue.Diagnostics());

                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(receivedData, 0, receivedData.Length);
                        ms.Seek(0, SeekOrigin.Begin);
                        receivedNumbers = (List <double>)bf.Deserialize(ms);
                    }
                    Assert.Equal(numbers, receivedNumbers);
                }
            }
        }
    }
}