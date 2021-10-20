using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DurableStorage.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DurableStorage.Functions.Orchestrators
{
    public static class AzureStorageOrchestrator
    {
        [FunctionName("AzureStorageOrchestrator")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            try
            {
                var uploadedCloudBlob = context.GetInput<CloudBlobItem>();
                bool isEmailSentToAdmin;

                //Chain #1 Send Message with BLOB details to Service Bus Queue. Returns the ServiceBus Message
                var serviceBusMessage = await context.CallActivityAsync<string>("SendMessageToServiceBusQueue", uploadedCloudBlob);

                if (serviceBusMessage != null)
                {
                    //Chain #2 Send SMS and call using TwilioAPI to set admin user that queue was updated with new blob
                    var isSmsSentAndCalledUser = await context.CallActivityAsync<bool>("SendSmsCallToTwilio", serviceBusMessage);

                    //Chain #3 send email using Sendgrid API
                    if (isSmsSentAndCalledUser)
                    {
                        isEmailSentToAdmin = await context.CallActivityAsync<bool>("SendEmailNotification", uploadedCloudBlob);
                    }

                    log.LogInformation($"A new cloud blob named {uploadedCloudBlob.Name} was uploaded to Azure Storage  " +
                        $"and added to service bus queue. \n" +
                        $" SMS sent = {isSmsSentAndCalledUser} to assigned user. \n" +
                        $" Access via BLOB URL: {uploadedCloudBlob.BlobUrl}" +
                        $" and email sent.");
                }

                return $"Done with the orchestration with Durable Context Id:  {context.InstanceId}";
            }
            catch (Exception ex)
            {
                //TODO Handle possible errors and do a retry if needed or retry a function
                log.LogError($"Something went wrong " + ex.Message);
                throw;
            }
        }      
    }
}