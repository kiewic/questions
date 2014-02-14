using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;

namespace QuestionsBackgroundTasks
{
    public sealed class ReadListManager
    {
        private const string ReadListKey = "ReadList";
        private const string FileName = "readList.json";
        private static StorageFolder storageFolder = ApplicationData.Current.RoamingFolder;
        private static JsonObject rootObject;
        private static JsonArray readList;

        private static IAsyncAction LoadAsync()
        {
            return AsyncInfo.Run(async (cancellationToken) =>
            {
                if (rootObject != null && readList != null)
                {
                    // File already loaded, there is nothing to do.
                    return;
                }

                string jsonString = await FilesManager.LoadAsync(storageFolder, FileName);

                if (!JsonObject.TryParse(jsonString, out rootObject))
                {
                    Debug.WriteLine("Invalid JSON object in {0}", FileName);
                    CreateFromScratch();
                    return;
                }

                if (!rootObject.ContainsKey(ReadListKey))
                {
                    CreateFromScratch();
                    return;
                }

                readList = rootObject.GetNamedArray(ReadListKey);
            });
        }

        private static void CreateFromScratch()
        {
            rootObject = new JsonObject();

            readList = new JsonArray();
            rootObject.Add(ReadListKey, readList);
        }

        public static void Unload()
        {
            rootObject = null;
            readList = null;
        }

        public static IAsyncAction SaveAsync()
        {
            Task saveTask = FilesManager.SaveAsync(storageFolder, FileName, rootObject.Stringify());
            return saveTask.AsAsyncAction();
        }

        internal static void AddReadQuestion(string questionId)
        {
            // TODO: Validate that a question is never maked as read twice? Why would that happend?
            readList.Add(JsonValue.CreateStringValue(questionId));
        }

        internal static async Task<JsonArray> GetReadListAsync()
        {
            if (readList == null)
            {
                await LoadAsync();
            }
            return readList;
        }

        internal static async void LimitTo450AndSave()
        {
            const int limit = 450;

            Debug.WriteLine("Read questions before limit: {0}", readList.Count);

            if (readList.Count > limit)
            {
                // Remove surplus questions.
                int arrayLength = readList.Count;
                JsonArray newReadList = new JsonArray();
                for (int arrayIndex = arrayLength - limit; arrayIndex < arrayLength; arrayIndex++)
                {
                    newReadList.Add(readList[arrayIndex]);
                }
                readList = newReadList;
            }

            Debug.WriteLine("Read questions after limit: {0}", readList.Count);

            await SaveAsync();
        }
    }
}
