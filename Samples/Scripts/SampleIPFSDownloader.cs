using AlephVault.Unity.IPFS.Types;
using UnityEngine;


namespace AlephVault.Unity.IPFS
{
    namespace Samples
    {
        public class SampleIPFSDownloader : MonoBehaviour
        {
            [SerializeField]
            private string apiEndpoint;
            
            [SerializeField]
            private string[] ipfsObjects;
            
            private void Start()
            {
                DownloadFiles();                
            }

            private async void DownloadFiles()
            {
                IPFSLoader loader = new IPFSLoader(apiEndpoint);
                foreach (string file in ipfsObjects)
                {
                    await loader.Download(file);
                }
            }
        }
    }
}