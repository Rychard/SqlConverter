using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace Converter.Logic.Helpers
{
    public static class ZipHelper
    {
        /// <summary>
        /// Creates a ZIP archive using the specified dictionary.  
        /// The keys in the dictionary are the paths of the files as they should appear in the archive.  
        /// The values in the dictionary are the absolute paths to the files on the local filesystem.
        /// </summary>
        public static void CreateZip(String archiveFilePath, Dictionary<String, String> zipContents)
        {
            FileStream fsOut = File.Create(archiveFilePath);
            var zipStream = new ZipOutputStream(fsOut);
            zipStream.SetLevel(9); // Compression Level: Valid range is 0-9, with 9 being the highest level of compression.

            foreach (var content in zipContents)
            {
                String archivePath = content.Key; // The location of the file as it appears in the archive.
                String filePath = content.Value; // The location of the file as it exists on disk.

                if (!File.Exists(filePath))
                {
                    continue; // Skip any non-existent files.
                }

                var fi = new FileInfo(filePath);

                string entryName = archivePath; // Makes the name in zip based on the folder
                entryName = ZipEntry.CleanName(entryName); // Removes drive from name and fixes slash direction
                var newEntry = new ZipEntry(entryName);
                newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity
                newEntry.Size = fi.Length;
                zipStream.PutNextEntry(newEntry);

                // Zip the file in buffered chunks
                // the "using" will close the stream even if an exception occurs
                var buffer = new byte[4096];
                using (FileStream streamReader = File.OpenRead(filePath))
                {
                    StreamUtils.Copy(streamReader, zipStream, buffer);
                }
                zipStream.CloseEntry();
            }

            zipStream.IsStreamOwner = true; // Makes the Close also Close the underlying stream
            zipStream.Close();
        }
    }
}
