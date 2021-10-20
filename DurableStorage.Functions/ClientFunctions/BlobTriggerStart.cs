using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DurableStorage.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DurableStorage.Functions.ClientFunctions
{
    public static class BlobTriggerStart
    {
        [FunctionName("BlobTriggerStart")]
        public static async Task HttpBlobStart(
        [BlobTrigger("photoscontainer/{name}", Connection = "StorageConnectionString")] CloudBlockBlob myCloudBlob, string name, ILogger log,
        [DurableClient] IDurableOrchestrationClient starter)
        {
            try
            {
                log.LogInformation($"Started orchestration trigged by BLOB trigger. A blob item with name = '{name}'");
                log.LogInformation($"BLOB Name {myCloudBlob.Name}");

                // Function input comes from the request content.
                if (myCloudBlob != null)
                {
                    var newUploadedBlobItem = new CloudBlobItem
                    {
                        Name = myCloudBlob.Name,
                        BlobUrl = myCloudBlob.Uri.AbsoluteUri.ToString(),
                        Metadata = (Dictionary<string, string>)myCloudBlob.Metadata,
                        FileType = myCloudBlob.BlobType.ToString(),
                        Size = myCloudBlob.Name.Length.ToString(),
                        ETag = myCloudBlob.Properties.ETag.ToString()
                    };

                    var instanceId = await starter.StartNewAsync("AzureStorageOrchestrator", newUploadedBlobItem);
                    log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
                }
                else
                {
                    log.LogError($"The blob was trigged but myCloudBlob was empty");
                }
            }
            catch (Exception ex)
            {
                //Errorhandling
                log.LogError("Something went wrong. Error : " + ex.InnerException);
                throw;
            }
        }
    }
}
