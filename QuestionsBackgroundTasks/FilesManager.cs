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
    internal class FilesManager
    {
        private static AutoResetEvent autoEvent = new AutoResetEvent(true);
        private static object settingsFileLock = new object();

        // This is a private method, and does not require synchronization. We assume caller is
        // already holding the lock.
        private static async Task<StorageFile> GetOrCreateFileAsync(string fileName)
        {
            StorageFile storageFile = null;

            try
            {
                Debug.WriteLine("File location: {0}", ApplicationData.Current.LocalFolder.Path);
                storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("File not found: {0}", fileName);
            }

            if (storageFile == null)
            {
                storageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName);
            }

            return storageFile;
        }

        public static async Task<string> LoadAsync(string fileName)
        {
            await LockAsync();
            try
            {
                StorageFile storageFile = await GetOrCreateFileAsync(fileName);

                string content = await FileIO.ReadTextAsync(storageFile);
                Debug.WriteLine("Text length: {0}", content.Length);
                return content;
            }
            finally
            {
                Unlock();
            }
        }

        public static async Task SaveAsync(string fileName, string content)
        {
            await LockAsync();
            try
            {
                StorageFile storageFile = await GetOrCreateFileAsync(fileName);
                Debug.WriteLine("Text length: {0}", content.Length);
                await FileIO.WriteTextAsync(storageFile, content);
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
