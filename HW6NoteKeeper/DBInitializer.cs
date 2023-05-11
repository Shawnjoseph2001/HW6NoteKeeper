using Azure.Storage.Blobs;

namespace HW6NoteKeeperSolution
{
    /// <summary>
    /// Initializes (seeds) the database with data
    /// </summary>
    /// <remarks>Step 7</remarks>
    public static class DbInitializer
    {
        /// <summary>
        /// Initializes the specified context with data
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="blobServiceClient">The blob service client.</param>

        public static void Initialize(NoteContext context, BlobServiceClient blobServiceClient)
        {
            // Check to see if there is any data in the customer table
            if (context.Notes!.Any())
            {
                // Customer table has data, nothing to do here
                return;
            }
            var groceryGuid = Guid.NewGuid();
            var giftSuppliesGuid = Guid.NewGuid();
            var valentinesGuid = Guid.NewGuid();
            var azureGuid = Guid.NewGuid();
            context.Notes!.Add(new Note("Running grocery Assigned list", "Milk\nEggs\nOranges",
                groceryGuid));
            context.Notes!.Add(new Note("Gift supplies\nnotes", "Tape & Wrapping Paper",
               giftSuppliesGuid));
            context.Notes!.Add(new Note("Valentine's Day Assigned gift\nideas", 
                "Chocolate, Diamonds, \nNewCar", valentinesGuid));
            context.Notes!.Add(new Note("Azure tips", 
                "portal.azure.com is a\nquick way to get to\nthe portal\nRemember double\nunderscore for " +
                "linux\nand colon for windows", azureGuid));      
            // Commit the changes to the database
            context.SaveChanges();
            var groceryBlob = blobServiceClient.GetBlobContainerClient(groceryGuid.ToString());
            groceryBlob.CreateIfNotExists();
            groceryBlob.GetBlobClient("MilkAndEggs.png").Upload("SampleAttachments/MilkAndEggs.png");
            groceryBlob.GetBlobClient("Oranges.png").Upload("SampleAttachments/Oranges.png");
            var giftSuppliesBlob = blobServiceClient.GetBlobContainerClient(giftSuppliesGuid.ToString());
            giftSuppliesBlob.CreateIfNotExists();
            giftSuppliesBlob.GetBlobClient("WrappingPaper.png").Upload("SampleAttachments/WrappingPaper.png");
            giftSuppliesBlob.GetBlobClient("Tape.png").Upload("SampleAttachments/Tape.png");
            var valentinesBlob = blobServiceClient.GetBlobContainerClient(valentinesGuid.ToString());
            valentinesBlob.CreateIfNotExists();
            valentinesBlob.GetBlobClient("Chocolate.png").Upload("SampleAttachments/Chocolate.png");
            valentinesBlob.GetBlobClient("Diamonds.png").Upload("SampleAttachments/Diamonds.png");
            valentinesBlob.GetBlobClient("NewCar.png").Upload("SampleAttachments/NewCar.png");
            var azureTipsBlob = blobServiceClient.GetBlobContainerClient(azureGuid.ToString());
            azureTipsBlob.CreateIfNotExists();
            azureTipsBlob.GetBlobClient("AzureLogo.png").Upload("SampleAttachments/AzureLogo.png");
            azureTipsBlob.GetBlobClient("AzureTipsAndTricks.pdf")
                .Upload("SampleAttachments/AzureTipsAndTricks.pdf");
        }
    }
}
