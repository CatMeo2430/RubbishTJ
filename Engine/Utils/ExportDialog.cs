using System;
using System.Windows;
using Microsoft.Win32;

namespace Taiji.Engine.Utils
{
    internal static class ExportDialog
    {
        public static string PromptSave(
            Window owner,
            string filter,
            string defaultFileName,
            Func<string, string> saver)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName ?? "export.png"
            };
            if (dialog.ShowDialog(owner) != true)
                return null;

            var err = saver(dialog.FileName);
            if (err != null)
                throw new InvalidOperationException(err);

            return dialog.FileName;
        }
    }
}
