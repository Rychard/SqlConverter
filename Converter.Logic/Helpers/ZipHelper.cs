using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
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
        /// <exception cref="UnauthorizedAccessException">The caller does not have the required permission.-or- <paramref name="archiveFilePath" /> specified a file that is read-only. </exception>
        /// <exception cref="PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length. For example, on Windows-based platforms, paths must be less than 248 characters, and file names must be less than 260 characters. </exception>
        /// <exception cref="DirectoryNotFoundException">The specified path is invalid (for example, it is on an unmapped drive). </exception>
        /// <exception cref="IOException">An I/O error occurred while creating the file. </exception>
        /// <exception cref="NotSupportedException"><paramref name="archiveFilePath" /> is in an invalid format. </exception>
        /// <exception cref="SecurityException">The caller does not have the required permission. </exception>
        /// <exception cref="FileNotFoundException">One of the elements within <paramref name="zipContents" /> points to a file that does not exist. </exception>
        public static void CreateZip(String archiveFilePath, Dictionary<String, String> zipContents)
        {
            using (FileStream fsOut = File.Create(archiveFilePath))
            {
                var zipStream = new ZipOutputStream(fsOut);
                zipStream.SetLevel(9); // Compression Level: Valid range is 0-9, with 9 being the highest level of compression.

                foreach (var content in zipContents)
                {
                    String archivePath = content.Key; // The location of the file as it appears in the archive.
                    String filePath = content.Value; // The location of the file as it exists on disk.

                    // Skip files that have no path.
                    if (String.IsNullOrWhiteSpace(filePath)) { continue; }

                    // Skip files that do not exist.
                    if (!File.Exists(filePath)) { continue; }

                    FileInfo fi = new FileInfo(filePath);

                    // Makes the name in zip based on the folder
                    String entryName = archivePath;

                    // Removes drive from name and fixes slash direction
                    entryName = ZipEntry.CleanName(entryName); 
                    
                    
                    var newEntry = new ZipEntry(entryName);
                    newEntry.DateTime = fi.LastWriteTime; // Note: Zip format stores 2 second granularity
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
                zipStream.Dispose();
            }
        }
    }
}
