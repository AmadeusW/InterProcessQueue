using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeConnect.MemoryMappedQueue.Tests
{
    public class MemoryMappedQueueTests
    {
        /// <summary>
        /// Since Xunit runs tests in parallel, we need to give
        /// memory mapped files different names. This method gives
        /// them name unique by the test file.
        /// </summary>
        protected string AccessName
        {
            get
            {
                return this.GetType().ToString();
            }
        }
    }
}
