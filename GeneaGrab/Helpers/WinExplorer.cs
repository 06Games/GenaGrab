using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GeneaGrab.Helpers;

public static class WinExplorer
{
    /// <summary>Opens a Windows Explorer window with specified items in a particular folder selected.</summary>
    /// <param name="pidlFolder">A pointer to a fully qualified item ID list that specifies the folder.</param>
    /// <param name="cidl">A count of items in the selection array, apidl. If cidl is zero, then pidl. Folder must point to a fully specified ITEMIDLIST describing a single item to select. This function opens the parent folder and selects that item.</param>
    /// <param name="apidl">A pointer to an array of PIDL structures, each of which is an item to select in the target folder referenced by pidlFolder.</param>
    /// <param name="dwFlags">The optional flags. Under Windows XP this parameter is ignored. In Windows Vista, the following flags are defined.</param>
    /// <returns>If this function succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, uint dwFlags);

    /// <summary>
    /// Translates a Shell namespace object's display name into an item identifier list and returns the attributes of the object. This function is the preferred method to convert a string to a pointer to an item identifier list (PIDL).
    /// </summary>
    /// <param name="name">A pointer to a zero-terminated wide string that contains the display name to parse.</param>
    /// <param name="bindingContext">A bind context that controls the parsing operation. This parameter is normally set to NULL.</param>
    /// <param name="pidl">The address of a pointer to a variable of type ITEMIDLIST that receives the item identifier list for the object. If an error occurs, then this parameter is set to NULL.</param>
    /// <param name="sfgaoIn">A ULONG value that specifies the attributes to query. To query for one or more attributes, initialize this parameter with the flags that represent the attributes of interest.</param>
    /// <param name="psfgaoOut">A pointer to a ULONG. On return, those attributes that are true for the object and were requested in sfgaoIn are set. An object's attribute flags can be zero or a combination of SFGAO flags.</param>
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindingContext, [Out] out IntPtr pidl, uint sfgaoIn, [Out] out uint psfgaoOut);


    /// <summary>Open folder and select an item</summary>
    /// <param name="filePath">Full path to a file to highlight in Windows Explorer</param>
    public static void OpenFolderAndSelectItem(string filePath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException("Only Windows is supported by this function");

        var folderPath = Path.GetDirectoryName(filePath);
        if (folderPath is null) return;

        SHParseDisplayName(folderPath, IntPtr.Zero, out var nativeFolder, 0, out _);
        if (nativeFolder == IntPtr.Zero) return; // Log error, can't find folder

        SHParseDisplayName(Path.Combine(folderPath, filePath), IntPtr.Zero, out var nativeFile, 0, out _);
        IntPtr[] fileArray = nativeFile == IntPtr.Zero ? [] : [nativeFile]; // Open the folder without the file selected if we can't find the file

        var hresult = SHOpenFolderAndSelectItems(nativeFolder, (uint)fileArray.Length, fileArray, 0);
        Marshal.ThrowExceptionForHR(hresult); // Throw any error that could have occured

        Marshal.FreeCoTaskMem(nativeFolder);
        if (nativeFile != IntPtr.Zero) Marshal.FreeCoTaskMem(nativeFile);
    }
}
