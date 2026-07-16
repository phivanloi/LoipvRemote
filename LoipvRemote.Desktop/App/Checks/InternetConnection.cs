using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LoipvRemote.App.Update
{
    public class InternetConnection
    {
        public static async Task<bool> IsPossibleAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                using HttpResponseMessage response = await client.GetAsync(
                    "https://www.microsoft.com",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }
    }
}
