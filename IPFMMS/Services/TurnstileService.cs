using System.Text.Json.Serialization;

namespace IPFMMS.Services
{
    public class TurnstileService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public TurnstileService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> VerifyTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            string? secretKey =
                _configuration["Turnstile:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
                return false;

            var formData = new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token
            };

            try
            {
                using var content = new FormUrlEncodedContent(formData);

                using var response = await _httpClient.PostAsync(
                    "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                    content);

                if (!response.IsSuccessStatusCode)
                    return false;

                var result = await response.Content
                    .ReadFromJsonAsync<TurnstileVerificationResponse>();

                return result?.Success == true;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }

        private sealed class TurnstileVerificationResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }
        }
    }
}