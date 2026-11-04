using Microsoft.AspNetCore.Http;

namespace IPFMMS.Services
{
    public interface IReceiptStorageService
    {
        Task<string> UploadAsync(
            IFormFile file,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string receiptPath,
            CancellationToken cancellationToken = default);

        Task<(Stream Content, string ContentType)> DownloadAsync(
            string receiptPath,
            CancellationToken cancellationToken = default);
    }
}