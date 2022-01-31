using System;
using System.IO;


namespace AlephVault.Unity.IPFS
{
    namespace Types
    {
        /// <summary>
        ///   Exception to be raised when the size of the object
        ///   about to be downloaded passes the allowed limit.
        /// </summary>
        public class DownloadSizeExceededException : IOException
        {
            public DownloadSizeExceededException(string message) : base(message) {}
        }
    }
}