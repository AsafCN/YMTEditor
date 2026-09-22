using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YMTEditor
{
    /// <summary>
    /// The Windows folder picker (the same one Explorer shows), so a path can be pasted
    /// into it. WinForms' FolderBrowserDialog on .NET Framework is still the old tree
    /// with no address box, and is used only if this one isn't available.
    /// </summary>
    public static class FolderPicker
    {
        public static string Pick(Window owner, string title, string startFolder)
        {
            try
            {
                return PickModern(owner, title, startFolder);
            }
            catch (Exception)
            {
                using (System.Windows.Forms.FolderBrowserDialog fallback = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fallback.Description = title;
                    fallback.ShowNewFolderButton = false;
                    if (!string.IsNullOrEmpty(startFolder))
                    {
                        fallback.SelectedPath = startFolder;
                    }
                    return fallback.ShowDialog() == System.Windows.Forms.DialogResult.OK ? fallback.SelectedPath : null;
                }
            }
        }

        private static string PickModern(Window owner, string title, string startFolder)
        {
            IFileOpenDialog dialog = (IFileOpenDialog)new FileOpenDialogRCW();
            uint options;
            dialog.GetOptions(out options);
            dialog.SetOptions(options | FosPickFolders | FosForceFileSystem);
            dialog.SetTitle(title);

            if (!string.IsNullOrEmpty(startFolder) && System.IO.Directory.Exists(startFolder))
            {
                try
                {
                    IShellItem start;
                    if (SHCreateItemFromParsingName(startFolder, IntPtr.Zero, typeof(IShellItem).GUID, out start) == 0
                        && start != null)
                    {
                        dialog.SetFolder(start);
                    }
                }
                catch (Exception)
                {
                    //not being able to start in that folder is no reason to give up on the dialog
                }
            }

            IntPtr handle = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            if (dialog.Show(handle) != 0)
            {
                return null; //cancelled
            }

            IShellItem result;
            dialog.GetResult(out result);
            string path;
            result.GetDisplayName(SigdnFileSysPath, out path);
            return path;
        }

        private const uint FosPickFolders = 0x00000020;
        private const uint FosForceFileSystem = 0x00000040;
        private const uint SigdnFileSysPath = 0x80058000;

        //riid is a REFIID: it has to arrive as a pointer to the guid. Passing the guid
        //by value reads whatever follows it as the pointer, which kills the process.
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr bc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRCW { }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes();          // not used, kept for vtable order
            void SetFileTypeIndex(uint i);
            void GetFileTypeIndex(out uint i);
            void Advise();
            void Unadvise();
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, int alignment);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
            void GetResults(out IntPtr items);
            void GetSelectedItems(out IntPtr items);
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
