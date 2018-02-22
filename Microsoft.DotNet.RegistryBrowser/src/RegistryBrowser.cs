using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.RegistryBrowser
{
    public class RegistryBrowser
    {
        private static HttpClient HttpClient { get; } = new HttpClient();
        private const string RegistryUrl = "https://hub.docker.com/v2/";

        public static async Task Main(string[] args)
        {
            await LoginAsync(args[0], args[1]);
            await GetLatestWindowsTags(args[2]);
        }

        private static async Task GetLatestWindowsTags(string owner)
        {
            JObject repos = await GetRepos(owner);
            Console.WriteLine("REPO, MAX RS1, MAX RS3, OTHER");
            foreach (JObject repo in repos.GetValue("results"))
            {
                string repoName = repo["name"].Value<string>();
                JObject tags = await GetTags("microsoft", repoName);
                IEnumerable<Version> windowsVersions = tags.GetValue("results")
                    .Cast<JObject>()
                    .SelectMany(tag => tag.GetValue("images"))
                    .Cast<JObject>()
                    .Where(image => image.GetValue("os").Value<string>() == "windows")
                    .Select(image => image.GetValue("os_version").Value<string>())
                    .Where(ver => !string.IsNullOrWhiteSpace(ver))
                    .Distinct()
                    .Select(version => new Version(version));
                if (windowsVersions.Any())
                {
                    string maxRs1 = string.Empty;
                    string maxRs3 = string.Empty;
                    string other = string.Empty;

                    IEnumerable<Version> rs1Versions = windowsVersions.Where(ver => ver.Build == 14393);
                    if (rs1Versions.Any())
                    {
                        maxRs1 = $"10.0.14393.{rs1Versions.Max(ver => ver.Revision)}";
                    }

                    IEnumerable<Version> rs3Versions = windowsVersions.Where(ver => ver.Build == 16299);
                    if (rs3Versions.Any())
                    {
                        maxRs3 = $"10.0.16299.{rs3Versions.Max(ver => ver.Revision)}";
                    }

                    IEnumerable<Version> otherVersions = windowsVersions.Where(ver => ver.Build != 16299 && ver.Build != 14393);
                    if (otherVersions.Any())
                    {
                        other = string.Join(", ", otherVersions);
                    }

                    Console.WriteLine($"{repoName}, {maxRs1}, {maxRs3}, {other}");
                }
            }
        }

        private static async Task LoginAsync(string username, string password)
        {
            HttpResponseMessage response = await HttpClient.PostAsync(
                $"{RegistryUrl}users/login",
                new StringContent(
                    $"{{\"username\": \"{username}\", \"password\": \"{password}\"}}",
                    Encoding.UTF8,
                    "application/json"));
            response.EnsureSuccessStatusCode();
            string responseContent = await response.Content.ReadAsStringAsync();
            JObject jsonResponse = JObject.Parse(responseContent);
            JValue token = (JValue)jsonResponse["token"];
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("JWT", token.Value<string>());
        }

        private static async Task<JObject> GetRepos(string owner)
        {
            HttpResponseMessage response = await HttpClient.GetAsync($"{RegistryUrl}repositories/{owner}/?page_size=1000");
            response.EnsureSuccessStatusCode();
            string responseContent = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseContent);
        }

        private static async Task<JObject> GetTags(string owner, string repo)
        {
            HttpResponseMessage response = await HttpClient.GetAsync($"{RegistryUrl}repositories/{owner}/{repo}/tags/?page_size=1000");
            response.EnsureSuccessStatusCode();
            string responseContent = await response.Content.ReadAsStringAsync();
            return JObject.Parse(responseContent);
        }
    }
}
