using System;


namespace AlephVault.Unity.IPFS
{
    namespace Types
    {
        /// <summary>
        ///   Accounts for the JSON data to de-serialize. Only accounts
        ///   for the data that we might consider relevant.
        /// </summary>
        [Serializable]
        public class NodeStats
        {
            /// <summary>
            ///   On success, the cumulative size.
            /// </summary>
            public ulong CumulativeSize;
            
            /// <summary>
            ///   On error, the error message.
            /// </summary>
            public string Message;
        }
    }
}