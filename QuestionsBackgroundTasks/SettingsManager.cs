using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace QuestionsBackgroundTasks
{
    internal class SettingsManager
    {
        private const string settingsFileName = "settings.json";
        private static AutoResetEvent autoEvent = new AutoResetEvent(true);
        private static object settingsFileLock = new object();
        private static StorageFile settingsFile;

        public static async Task<string> LoadAsync()
        {
            await LockAsync();

            try
            {
                bool fileFound = false;

                try
                {
                    Debug.WriteLine("Settings file location: " + ApplicationData.Current.LocalFolder.Path);
                    settingsFile = await ApplicationData.Current.LocalFolder.GetFileAsync(settingsFileName);
                    fileFound = true;
                }
                catch (FileNotFoundException)
                {
                    Debug.WriteLine("Settings file not found.");
                }

                if (!fileFound)
                {
                    settingsFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(settingsFileName);
                }
                else
                {
                    string content = await FileIO.ReadTextAsync(settingsFile);
                    return content;
                }
            }
            finally
            {
                Unlock();
            }

            return "";
        }

        public static async Task SaveAsync(string content)
        {
            await LockAsync();

            try
            {
                Debug.WriteLine("Content length: {0}", content.Length);
                await FileIO.WriteTextAsync(settingsFile, content);
            }
            finally
            {
                Unlock();
            }
        }

        // TODO: Interesting. Is there a better way to add synchronization?
        private static Task<bool> LockAsync()
        {
            return Task.Run(() =>
            {
                // Lock.
                bool result = autoEvent.WaitOne();
                if (!result)
                {
                    Debugger.Break();
                }
                return result;
            });
        }

        private static bool Unlock()
        {
            bool result = autoEvent.Set();
            if (!result)
            {
                Debugger.Break();
            }
            return result;
        }
    }
}
