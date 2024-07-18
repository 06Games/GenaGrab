using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.UI.Shell.Common;

namespace GeneaGrab.Helpers;

public static partial class FileExplorer
{
    /// <summary>Open folder and select an item</summary>
    /// <param name="fileInfo">The info of the file to highlight in the File Explorer</param>
    public static void OpenFolderAndSelectItem(FileInfo fileInfo)
    {
        var folderPath = fileInfo.DirectoryName;
        if (folderPath != null) OpenFolderAndSelectItem(folderPath, fileInfo.Name);
    }

    /// <summary>Open folder and select an item</summary>
    /// <param name="folderPath">Full path to the folder to open</param>
    /// <param name="fileName">Name of the file to highlight in the File Explorer (if supported)</param>
    private static void OpenFolderAndSelectItem(string folderPath, string fileName)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            OpenFolderAndSelectItemWindows5_1_2600(folderPath, fileName); // Will reuse the existing window if the folder is already opened
        else if (OperatingSystem.IsWindows())
            Process.Start("explorer.exe", $"/select,\"{Path.Combine(folderPath, fileName)}\""); // Will open a new explorer window at each call
        else
            Process.Start(new ProcessStartInfo { FileName = new Uri(folderPath).AbsoluteUri, UseShellExecute = true });
    }

    [SupportedOSPlatform("windows5.1.2600")]
    private static unsafe void OpenFolderAndSelectItemWindows5_1_2600(string folderPath, string fileName)
    {
        PInvoke.SHParseDisplayName(folderPath, null, out var nativeFolder, 0, null);
        if (nativeFolder == null) return; // Log error, can't find folder

        PInvoke.SHParseDisplayName(Path.Combine(folderPath, fileName), null, out var nativeFile, 0, null);
        ITEMIDLIST*[] fileArray = nativeFile == null ? [] : [nativeFile]; // Open the folder without the file selected if we can't find the file
        fixed (ITEMIDLIST** fileArrayPtr = fileArray)
        {
            var hResult = PInvoke.SHOpenFolderAndSelectItems(nativeFolder, (uint)fileArray.Length, fileArrayPtr, 0);
            Marshal.ThrowExceptionForHR(hResult); // Throw any error that could have occured
        }
        Marshal.FreeCoTaskMem((nint)nativeFolder);
        if (nativeFile != null) Marshal.FreeCoTaskMem((nint)nativeFile);
    }
}
