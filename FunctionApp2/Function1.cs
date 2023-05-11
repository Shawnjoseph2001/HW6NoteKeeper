/*
This Azure Function project provides functionality to create a zip file from a collection of blobs in Azure Blob Storage. 
The function is triggered by a message in an Azure Storage Queue and processes the blobs associated with the specified noteId.
 */
using Azure.Storage.Blobs;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FunctionApp2
{
    /// <summary>
    /// Represents an Azure Function that processes blobs in Azure Blob Storage to create a zip file.
    /// </summary>
    public class Function1
    {
        private readonly ILogger log;
        private readonly string ConnectionString;
        /// <summary>
        /// Initializes a new instance of the <see cref="Function1"/> class.
        /// </summary>
        /// <param name="loggerFactory">An <see cref="ILoggerFactory"/> instance to create loggers.</param>
        /// <param name="configuration">An <see cref="IConfiguration"/> instance to access configuration settings.</param>
        public Function1(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            log = loggerFactory.CreateLogger<Function1>();
            ConnectionString = configuration.GetConnectionString("Storage");
        }
        /// <summary>
        /// Processes blobs in Azure Blob Storage and creates a zip file from the specified noteId.
        /// </summary>
        /// <param name="myQueueItem">A JSON-formatted string containing the noteId and zipFileId information.</param>
        [Function("Function1")]
        public void Run([QueueTrigger("attachment-zip-requests", Connection = "Storage")] string myQueueItem)
        {
            Console.WriteLine("QueueTrigger run!");
            log.LogInformation("C# Queue trigger function processed: {MyQueueItem}", myQueueItem);
            var info = JsonSerializer.Deserialize<Dictionary<string, string>>(myQueueItem);
            // Check for the presence of required keys in the JSON string
            if (info == null || !info.ContainsKey("noteId") || !info.TryGetValue("zipFileId", out var value))
                return;
            // Check for the presence of required keys in the JSON string
            var blobServiceClient = new BlobServiceClient(ConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(info["noteId"] + "-zip");
            var zipFilesClient = blobServiceClient.GetBlobContainerClient(info["noteId"]);
            // Check for the presence of required keys in the JSON string
            var memoryStream = new MemoryStream();
            var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create);
            // Create the container for the zip files if it does not exist
            zipFilesClient.CreateIfNotExists();
            // Iterate through blobs and add them to the ZipArchive
            foreach (var i in zipFilesClient.GetBlobs())
            {
                if (i.Deleted) continue;
                var downloadblobClient = zipFilesClient.GetBlobClient(i.Name);
                var archiveEntry = archive.CreateEntry(downloadblobClient.Name).Open();
                var response = downloadblobClient.Download();
                response.Value.Content.CopyTo(archiveEntry);
                archiveEntry.Close();
            }
            // Iterate through blobs and add them to the ZipArchive
            blobContainerClient.CreateIfNotExists();
            // Reset the memoryStream position and upload the zip file to the target container
            memoryStream.Position = 0;
            var blobClient = blobContainerClient.GetBlobClient(value);
            blobClient.Upload(memoryStream);
        }
    }
}
