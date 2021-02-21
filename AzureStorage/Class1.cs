using System;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using EventSourceingCoreStuff;
using Newtonsoft.Json;

namespace AzureStorage
{
    public class RawStorageProvider : IRawStorageProvider
    {
        private readonly string _connectionString;

        public RawStorageProvider(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<T> GetMementoAsync<T>(string id)
        {
            return await ReadFromAzureBlob<T>("snapshots", $"{typeof(T).Name}.{id}.json");
        }

        public async Task StashMementoAsync(IMemento memento, string id)
        {
            var json = JsonConvert.SerializeObject(memento);
            await UploadToAzureBlob("snapshots", $"{memento.GetType().Name}.{id}.json",json);
        }

        public async Task UploadToAzureBlob(string containerName, string path, string contents)
        {
            BlobContainerClient container = new BlobContainerClient(_connectionString, containerName);
            await container.CreateIfNotExistsAsync();
            
            var array = UTF8Encoding.UTF8.GetBytes(contents);
            using (var ms = new MemoryStream(array))
            {
                await container.UploadBlobAsync(path, ms);
            }
        }

        public async Task<T> ReadFromAzureBlob<T>(string containerName, string path)
        {
            BlobContainerClient container = new BlobContainerClient(_connectionString, containerName);
            await container.CreateIfNotExistsAsync();

            var data = await container.GetBlobClient(path)
                .DownloadAsync();

            using (var ms = new MemoryStream())
            {
                data.Value.Content.CopyTo(ms);
                var json = UTF8Encoding.UTF8.GetString(ms.ToArray());
                return JsonConvert.DeserializeObject<T>(json);
            }
        }
    }
}
