using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;

namespace IPFMMS.Services
{
    public class AzureReceiptStorageService
        : IReceiptStorageService
    {
        private readonly BlobContainerClient _container;

        private const long MaxFileSize = 5 * 1024 * 1024;

        public AzureReceiptStorageService(
            IConfiguration configuration)
        {
            string connectionString =
                configuration["ReceiptStorage:ConnectionString"]
                ?? configuration["AzureBlobStorage:ConnectionString"]
                ?? throw new InvalidOperationException(
                    "Azure Blob Storage connection string is missing.");

            string containerName =
                configuration["ReceiptStorage:ContainerName"]
                ?? configuration["AzureBlobStorage:ContainerName"]
                ?? "receipts";

            _container = new BlobContainerClient(
                connectionString,
                containerName);
        }

        public async Task<string> UploadAsync(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (file.Length <= 0 ||
                file.Length > MaxFileSize)
            {
                throw new ArgumentException(
                    "Receipt must be between 1 byte and 5 MB.");
            }

            string extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            string contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                _ => throw new ArgumentException(
                    "Unsupported receipt format.")
            };

            // Blob containers are private by default.
            // Do not enable anonymous public access.
            await _container.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            string blobName =
                $"{Guid.NewGuid():N}" +
                (extension == ".jpeg" ? ".jpg" : extension);

            BlobClient blobClient =
                _container.GetBlobClient(blobName);

            await using Stream stream =
                file.OpenReadStream();

            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType
                    }
                },
                cancellationToken);

            return blobName;
        }

        public async Task DeleteAsync(
            string receiptPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(receiptPath))
                return;

            BlobClient blobClient =
                _container.GetBlobClient(receiptPath);

            await blobClient.DeleteIfExistsAsync(
                cancellationToken: cancellationToken);
        }

        public async Task<(Stream Content, string ContentType)> DownloadAsync(
    string receiptPath,
    CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(receiptPath))
            {
                throw new ArgumentException(
                    "Receipt path is required.",
                    nameof(receiptPath));
            }

            string fileName = Path.GetFileName(receiptPath);

            if (fileName != receiptPath)
            {
                throw new ArgumentException(
                    "Invalid receipt path.",
                    nameof(receiptPath));
            }

            var blobClient = _container.GetBlobClient(fileName);

            var response = await blobClient.DownloadStreamingAsync(
                cancellationToken: cancellationToken);

            string contentType =
                response.Value.Details.ContentType
                ?? "application/octet-stream";

            return (response.Value.Content, contentType);
        }
    }
}