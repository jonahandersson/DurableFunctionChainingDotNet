using Azure.Messaging.ServiceBus;
using DurableStorage.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DurableStorage.Functions.ActivityFunctions
{
    public static class ReceiveUploadedBlobFromServiceBusQueue
    {
        [FunctionName("ReceiveUploadedBlobFromServiceBus")]
        public static async Task<List<ImageUploadLog>> ReceiveUploadedBlobFromServiceBus([ActivityTrigger] CloudBlobItem uploadBlob, ILogger log, ExecutionContext executionContext)
        {
            List<ImageUploadLog> uploadedImages = new List<ImageUploadLog>();

            //Config for Azure Service Bus
            var azureServiceBusConfig = new ConfigurationBuilder()
                 .SetBasePath(executionContext.FunctionAppDirectory)
                 .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                 .AddEnvironmentVariables()
                 .Build();

            var serviceBusConnection = azureServiceBusConfig["AzureServiceBusConnectionString"];
            var serviceBusQueue = azureServiceBusConfig["ServiceBusQueueName"];

            var sendGridAPIKey = azureServiceBusConfig["SendGridAPIKey"];
            var adminEmail = azureServiceBusConfig["Admin_Email"];
            var adminName = azureServiceBusConfig["Admin_Name"];

            try
            {
                if (uploadBlob != null)
                {
                    log.LogInformation($"Azure Service Bus Queue was updated with new uploaded BLOB. Receiving uploaded data from Azure Service Bus");

                    //Received queue message date on triggered event uploaded blob
                    await using (ServiceBusClient client = new ServiceBusClient(serviceBusConnection))
                    {
                        //ServiceBus Processor options
                        var serviceBusProcessorOptions = new ServiceBusProcessorOptions
                        {
                            AutoCompleteMessages = false,
                            MaxConcurrentCalls = 2,
                            ReceiveMode = ServiceBusReceiveMode.PeekLock
                        };

                        // Create a message processor to process queue message
                        ServiceBusProcessor serviceBusProcessor = client.CreateProcessor(serviceBusQueue, serviceBusProcessorOptions);
                        TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        //Queue message handler
                        serviceBusProcessor.ProcessMessageAsync += QueueMessageHandler;

                        //process possible errors
                        serviceBusProcessor.ProcessErrorAsync += ServiceBusQueueMessageErrorHandler;

                        //Async task to handle receiving AzureService Bus queue messages in the cloud
                        async Task QueueMessageHandler(ProcessMessageEventArgs args)
                        {
                            string queueMessageBody = Encoding.UTF8.GetString(args.Message.Body);
                            log.LogInformation($"Received Message Service Bus Queue: {queueMessageBody}");

                            uploadedImages.Add(new ImageUploadLog
                            {
                                Id = new Guid(),
                                Message = queueMessageBody,
                                ImageUrl = uploadBlob.BlobUrl,
                                ImageName = uploadBlob.Name
                            });

                            log.LogInformation($"Received Message Service Bus Queue was added to uploadedImages list");
                            await args.CompleteMessageAsync(args.Message);
                            taskCompletionSource.SetResult(true);
                        }

                        //Error message handler
                        Task ServiceBusQueueMessageErrorHandler(ProcessErrorEventArgs args)
                        {
                            log.LogError($"Error getting message from Service Bus Queue: {args.Exception}");
                            return Task.CompletedTask;
                        }

                        //Start processing
                        await serviceBusProcessor.StartProcessingAsync();

                        //Await task completion
                        await taskCompletionSource.Task;

                        //Manually stop receiving message from Service Bus Queue if error occurs
                        await serviceBusProcessor.StopProcessingAsync();
                        return uploadedImages;
                    }
                }
                else
                {
                    log.LogInformation($"Azure Service Bus Queue is not updated with new data");
                    return new List<ImageUploadLog>();
                }
            }
            catch (Exception ex)
            {
                log.LogInformation($"Something went wrong. Exception : {ex.InnerException}. Stopping process of receiving messages");
                throw;
            }
        }
    }
}
