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
        private static async Task<StorageFile> GetOrCreateFileAsync(StorageFolder storageFolder, string fileName)
        {
            StorageFile storageFile = null;

            try
            {
                Debug.WriteLine("File location: {0}", storageFolder.Path);
                storageFile = await storageFolder.GetFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine("File not found: {0}", fileName);
            }

            if (storageFile == null)
            {
                storageFile = await storageFolder.CreateFileAsync(fileName);
            }

            return storageFile;
        }

        public static async Task<string> LoadAsync(StorageFolder storageFolder, string fileName)
        {
            await LockAsync();
            try
            {
                StorageFile storageFile = await GetOrCreateFileAsync(storageFolder, fileName);

                string content = await FileIO.ReadTextAsync(storageFile);
                Debug.WriteLine("Length of read text: {0}", content.Length);
                return content;
            }
            finally
            {
                Unlock();
            }
        }

        public static async Task SaveAsync(StorageFolder storageFolder, string fileName, string content)
        {
            Debug.WriteLine("SaveAsync of {0} started.", fileName);
            await LockAsync();
            try
            {
                StorageFile storageFile = await GetOrCreateFileAsync(storageFolder, fileName);
                Debug.WriteLine("Length of written text: {0}", content.Length);
                await FileIO.WriteTextAsync(storageFile, content);
            }
            finally
            {
                Unlock();
            }
            Debug.WriteLine("SaveAsync of {0} completed.", fileName);
        }

        public static async Task<bool> FileExistsAsync(StorageFolder storageFolder, string fileName)
        {
            await LockAsync();
            try
            {
                try
                {
                    StorageFile storageFile = await storageFolder.GetFileAsync(fileName);
                    return true;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
            }
            finally
            {
                Unlock();
            }
        }

        public static async Task DeleteAsync(StorageFolder storageFolder, string fileName)
        {
            await LockAsync();
            try
            {
                StorageFile storageFile = await GetOrCreateFileAsync(storageFolder, fileName);
                await storageFile.DeleteAsync();
            }
            finally
            {
                Unlock();
            }
        }

        public static async Task MoveAsync(StorageFolder sourceFolder, StorageFolder destinationFolder, string fileName)
        {
            await LockAsync();
            try
            {
                StorageFile storageFile = await GetOrCreateFileAsync(sourceFolder, fileName);
                await storageFile.MoveAsync(destinationFolder);
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
