using System.Runtime.InteropServices;
using System.Text;
using WinRT.Interop;

namespace UserTrace.Helpers;

/// <summary>
/// Diálogo "Salvar como" nativo do Windows (comdlg32), confiável em apps WinUI não empacotados.
/// </summary>
internal static class NativeSaveFileDialog
{
    private const int MaxPath = 260;
    private const int OfnExplorer = 0x00080000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnOverwritePrompt = 0x00000002;
    private const int OfnHideReadOnly = 0x00000004;

    public static string? Show(Microsoft.UI.Xaml.Window window, string fileName, string formato)
    {
        var filter = formato switch
        {
            "txt" => "Documento de Texto (*.txt)\0*.txt\0Todos os arquivos (*.*)\0*.*\0",
            "docx" => "Documento Word (*.docx)\0*.docx\0Todos os arquivos (*.*)\0*.*\0",
            "pdf" => "Documento PDF (*.pdf)\0*.pdf\0Todos os arquivos (*.*)\0*.*\0",
            _ => "Todos os arquivos (*.*)\0*.*\0"
        };

        var filterPtr = Marshal.StringToHGlobalUni(filter);
        var filePtr = Marshal.AllocHGlobal(MaxPath * sizeof(char));
        var titlePtr = Marshal.StringToHGlobalUni("Exportar resultado");
        var defExtPtr = Marshal.StringToHGlobalUni(formato);

        try
        {
            // Zera o buffer e copia o nome sugerido.
            Marshal.Copy(new byte[MaxPath * sizeof(char)], 0, filePtr, MaxPath * sizeof(char));

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var nameBytes = Encoding.Unicode.GetBytes(fileName + '\0');
                var copyLen = Math.Min(nameBytes.Length, MaxPath * sizeof(char));
                Marshal.Copy(nameBytes, 0, filePtr, copyLen);
            }

            var ofn = new NativeOpenFileName
            {
                lStructSize = Marshal.SizeOf<NativeOpenFileName>(),
                hwndOwner = WindowNative.GetWindowHandle(window),
                lpstrFilter = filterPtr,
                lpstrFile = filePtr,
                nMaxFile = MaxPath,
                lpstrTitle = titlePtr,
                Flags = OfnExplorer | OfnPathMustExist | OfnOverwritePrompt | OfnHideReadOnly,
                lpstrDefExt = defExtPtr,
                nFilterIndex = 1
            };

            if (!GetSaveFileNameW(ref ofn))
                return null;

            return Marshal.PtrToStringUni(filePtr);
        }
        finally
        {
            Marshal.FreeHGlobal(filterPtr);
            Marshal.FreeHGlobal(filePtr);
            Marshal.FreeHGlobal(titlePtr);
            Marshal.FreeHGlobal(defExtPtr);
        }
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileNameW(ref NativeOpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeOpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }
}
