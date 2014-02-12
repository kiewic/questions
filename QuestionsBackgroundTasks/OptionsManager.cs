using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace QuestionsBackgroundTasks
{
    public sealed class OptionsManager
    {
        private static StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
        private const string websitesFileName = "sites.json";
        private const string websitesUriString = "http://api.stackexchange.com/2.1/sites?page=1&pagesize=999&filter=!)Qk)IzwPu2_4AJke)ujE)iqv&status=401";
        private const string tagsUriString = "http://api.stackexchange.com/2.1/tags?page=1&pagesize=100&order=desc&sort=popular&site={0}&filter=!6UYchuBldenIr";
        private const string emergencyJsonString = "{\"site_state\":\"normal\",\"favicon_url\":\"http://cdn.sstatic.net/stackoverflow/img/favicon.ico\",\"icon_url\":\"http://cdn.sstatic.net/stackoverflow/img/apple-touch-icon.png\",\"audience\":\"professional and enthusiast programmers\",\"site_url\":\"http://stackoverflow.com\",\"api_site_parameter\":\"stackoverflow\",\"name\":\"Stack Overflow\",\"site_type\":\"main_site\"}";

        public static IAsyncAction LoadAndDisplayWebsitesAsync(ListView listView)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                string content = await FilesManager.LoadAsync(storageFolder, websitesFileName);

                if (String.IsNullOrEmpty(content))
                {
                    // Get content from the web.
                    content = await LoadFromWeb(websitesUriString);

                    if (String.IsNullOrEmpty(content))
                    {
                        // No content found, there is nothing else to do.
                        return;
                    }

                    // Save it locally.
                    await FilesManager.SaveAsync(storageFolder, websitesFileName, content);
                }

                // Parse content.
                JsonObject jsonObject;
                if (!JsonObject.TryParse(content, out jsonObject))
                {
                    Debug.WriteLine("Invalid JSON object: {0}", content);
                    return;
                }

                if (!jsonObject.ContainsKey("items"))
                {
                    Debug.WriteLine("No items value.");
                    return;
                }

                JsonArray websitesArray = jsonObject.GetNamedArray("items");

                foreach (IJsonValue jsonValue in websitesArray)
                {
                    var option = new BindableWebsiteOption(jsonValue.GetObject());
                    if (option.IsListable)
                    {
                        listView.Items.Add(option);
                    }
                }
            });
        }

        public static IAsyncAction LoadAndDisplayTagOptionsAsync(ListView listView, string apiSiteParameter)
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                // Create a unique file name per website.
                string tagsFileName = "tags." + apiSiteParameter + ".json";
                string content = await FilesManager.LoadAsync(storageFolder, tagsFileName);

                if (String.IsNullOrEmpty(content))
                {
                    // Get content from the web.
                    string customUriString = String.Format(tagsUriString, apiSiteParameter);
                    content = await LoadFromWeb(customUriString);

                    if (String.IsNullOrEmpty(content))
                    {
                        // No content found, there is nothing else to do.
                        return;
                    }

                    // Save it locally.
                    await FilesManager.SaveAsync(storageFolder, tagsFileName, content);
                }

                // Parse content.
                JsonObject jsonObject;
                if (!JsonObject.TryParse(content, out jsonObject))
                {
                    Debug.WriteLine("Invalid JSON oject: {0}", content);
                    return;
                }

                if (!jsonObject.ContainsKey("items"))
                {
                    Debug.WriteLine("No items value.");
                    return;
                }

                JsonArray tagsArray = jsonObject.GetNamedArray("items");

                foreach (IJsonValue jsonValue in tagsArray)
                {
                    var option = new BindableTagOption(jsonValue.GetObject());
                    listView.Items.Add(option);
                }
            });
        }

        private static async Task<string> LoadFromWeb(string uriString)
        {
            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                handler.AutomaticDecompression = DecompressionMethods.GZip;
                HttpClient httpClient = new HttpClient(handler);
                string content = await httpClient.GetStringAsync(new Uri(uriString));
                return content;
            }
            catch (HttpRequestException ex)
            {
                // TODO: Display an error message to the user.
                Debug.WriteLine(ex);
            }

            return "";
        }

        // This method should be called when the StackExchange API refuses to pprovide data.
        public static void LoadAndDisplayEmergencyWebsites(ListView listView)
        {
            JsonObject jsonObject = JsonObject.Parse(emergencyJsonString);
            var option = new BindableWebsiteOption(jsonObject);
            if (option.IsListable)
            {
                listView.Items.Add(option);
            }
        }
    }
}
