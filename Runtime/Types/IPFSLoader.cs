using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AlephVault.Unity.Support.Types.Async;
using UnityEngine;
using UnityEngine.Networking;


namespace AlephVault.Unity.IPFS
{
    namespace Types
    {
        public class IPFSLoader
        {
            // The base path for any path to be used as IPFS downloaded resources root.
            private static string BasePath = Application.persistentDataPath;

            // The default path, when not provided, for the IPFS downloaded resources root.
            private static string DefaultPath = Path.Combine(BasePath, "IPFS-Cache");

            /// <summary>
            ///   The internal API endpoint. By default, http://localhost:5001.
            /// </summary>
            public readonly string APIEndpoint;
            
            /// <summary>
            ///   The download directory for this loader.
            /// </summary>
            public readonly string DownloadRoot;

            /// <summary>
            ///   Creates a loader, by letting the user specify custom endpoints
            ///   for both the gateway AND the API.
            /// </summary>
            /// <param name="gatewayEndpoint">The gateway to use. By default, http://localhost:8080</param>
            /// <param name="apiEndpoint">The API endpoint to use. By default, http://localhost:5001</param>
            public IPFSLoader(
                string apiEndpoint = "http://localhost:5001",
                string downloadRoot = null
            )
            {
                APIEndpoint = CleanURI(apiEndpoint, "api endpoint");
                DownloadRoot = CleanDownloadRoot(downloadRoot);
            }

            // Validates and normalizes the root to use. a null path stands for Resources/IPFS-Cache,
            private string CleanDownloadRoot(string downloadRoot)
            {
                if (string.IsNullOrEmpty(downloadRoot))
                {
                    downloadRoot = DefaultPath;
                }
                else
                {
                    downloadRoot = Path.GetFullPath(Path.Combine(BasePath, downloadRoot));
                    if (downloadRoot != BasePath || !downloadRoot.StartsWith(BasePath + Path.PathSeparator))
                    {
                        throw new ArgumentException($"The download root must be null or be a " +
                                                    $"child of {BasePath}");
                    }
                }
                
                return downloadRoot;
            }

            // Checks and cleans an URI. First, requires it to be http or https, then
            // requires it to not have fragment or query (it is allowed for the uri
            // to have a path, however). Finally, returns it right-slash-trimmed.
            private string CleanURI(string url, string property)
            {
                if (string.IsNullOrEmpty(url))
                {
                    return "http://localhost:5001";
                }
                
                Uri uri;
                try
                {
                    uri = new Uri(url);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Invalid {property} uri: It is not an url");
                }
                
                if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    throw new ArgumentException($"Invalid {property} uri: It must be http or https");
                }

                if (uri.Query != "" || uri.Fragment != "")
                {
                    throw new ArgumentException($"Invalid {property} uri: It must not have querystring nor " +
                                                $"hash/fragment");
                }

                return uri.AbsoluteUri.TrimEnd('/');
            }
            
            /// <summary>
            ///   Downloads an /ipfs/{cid} or /ipfs/{cid}/path... content
            ///   into the <see cref="DownloadRoot"/> directory.
            /// </summary>
            /// <param name="ipfsPath"></param>
            /// <exception cref="IOException"></exception>
            public async Task Download(string ipfsPath)
            {
                // Null ipfs paths make no sense.
                if (ipfsPath == null) throw new ArgumentNullException(nameof(ipfsPath));

                // Let's trim the value, both by removing spaces and trailing slash.
                ipfsPath = ipfsPath.TrimEnd('/').Trim();
                
                // The path must begin with /ipfs/.
                if (!ipfsPath.StartsWith("/ipfs/")) throw new ArgumentException(
                    "ipfsPath must begin with /ipfs/"
                );

                Debug.Log($"Downloading object {ipfsPath} to: {DownloadRoot}");
                
                // The only supported path type is /ipfs/{cid}, being any {cid}
                // in any recognized version, and an optional /path...
                Uri apiEndpointUri = new Uri(APIEndpoint);
                UriBuilder apiEndpointUriBuilder = new UriBuilder(apiEndpointUri);
                apiEndpointUriBuilder.Path = "api/v0/get";
                apiEndpointUriBuilder.Query = "?arg=" + ipfsPath;
                
                using (UnityWebRequest request = UnityWebRequest.Post(apiEndpointUriBuilder.Uri, new Dictionary<string, string>()))
                {
                    // Do the whole request.
                    await request.SendWebRequest();
                    if (request.responseCode != 200)
                    {
                        // Result is either 200 or 500. We use 500 for an IOException.
                        throw new IOException($"Path {ipfsPath} not found or not available");
                    }
                    
                    // The next thing to do is determine the download directory. The first
                    // thing to do is to remove the /ipfs/ prefix. Up to this point, at
                    // least one part other than "ipfs/" will exist in the path, so we
                    // discard the last element of the whole path (the worst case: the
                    // prefix path has no elements and goes directly under DownloadRoot).
                    string[] ipfsPathParts = ipfsPath.Substring(6).Split('/');
                    Array.Resize(ref ipfsPathParts, ipfsPathParts.Length - 1);
                    // Get the complete directory name.
                    string outputDirectory = Path.Combine(DownloadRoot, string.Join("/", ipfsPathParts));
                    // Now, all the sub-directories must be created.
                    // Directory.CreateDirectory(outputDirectory);
                    // The contents of the tar.gz-downloaded file will be unpacked into
                    // this new directory. The contents will be 1 single root node, which
                    // will match the omitted part of the path.

                    // The result comes in .tar.gz format, but the Content-Type comes text/plain.
                    // Get the bytes and turn it into a stream.
                    Stream stream = new MemoryStream(request.downloadHandler.data);

                    // Continue with this (after they answer me in StackOverflow):
                    // https://gist.github.com/ForeverZer0/a2cd292bd2f3b5e114956c00bb6e872b
                    ExtractTar(stream, outputDirectory);
                }
            }
            
            // Extracts a tar archive to the specified directory.
            private static void ExtractTar(Stream stream, string outputDir)
            {
                var buffer = new byte[100];
                while (true)
                {
                    // Get the name.
                    stream.Read(buffer, 0, 100);
                    var name = Encoding.ASCII.GetString(buffer).Trim('\0');
                    if (String.IsNullOrWhiteSpace(name)) break;
                    
                    stream.Seek(24, SeekOrigin.Current);
                    stream.Read(buffer, 0, 12);
                    var size = Convert.ToInt64(Encoding.UTF8.GetString(buffer, 0, 12).Trim('\0').Trim(), 8);
                    
                    // Get the file type. Only files and directories are supported.
                    stream.Seek(20, SeekOrigin.Current);
                    byte typeFlag = (byte)stream.ReadByte();
                    if (typeFlag != 0 && typeFlag != 48 && typeFlag != 53) continue;

                    stream.Seek(355L, SeekOrigin.Current);

                    var entry = Path.Combine(outputDir, name);
                    var entryDirectory = Path.GetDirectoryName(entry);
                    if (!Directory.Exists(entryDirectory)) Directory.CreateDirectory(entryDirectory);
                    
                    if (!name.Equals("./", StringComparison.InvariantCulture)) 
                    {
                        if (typeFlag == 53)
                        {
                            Directory.CreateDirectory(entry);
                        }
                        else
                        {
                            using (var str = File.Open(entry, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                var buf = new byte[size];
                                stream.Read(buf, 0, buf.Length);
                                str.Write(buf, 0, buf.Length);
                            }
                        }
                    }

                    var pos = stream.Position;
	
                    var offset = 512 - (pos  % 512);
                    if (offset == 512)
                        offset = 0;

                    stream.Seek(offset, SeekOrigin.Current);
                }
            }
        }
    }
}
