namespace HVAC_Pro_Desktop.Services
{
    // Preserves WinForms alert behavior while localizing only presentation text and captions.
    internal static class LocalizedMessageBox
    {
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.Boolean displayHelpButton)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, displayHelpButton);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath, System.String keyword)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath, keyword);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath, System.String keyword)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath, keyword);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath, System.Windows.Forms.HelpNavigator navigator)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath, navigator);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath, System.Windows.Forms.HelpNavigator navigator)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath, navigator);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath, System.Windows.Forms.HelpNavigator navigator, System.Object param)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath, navigator, param);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options, System.String helpFilePath, System.Windows.Forms.HelpNavigator navigator, System.Object param)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options, helpFilePath, navigator, param);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons);
        public static System.Windows.Forms.DialogResult Show(System.String text, System.String caption)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text), LanguageManager.Get(caption));
        public static System.Windows.Forms.DialogResult Show(System.String text)
            => System.Windows.Forms.MessageBox.Show(LanguageManager.GetMessage(text));
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton, System.Windows.Forms.MessageBoxOptions options)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton, options);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon, System.Windows.Forms.MessageBoxDefaultButton defaultButton)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon, defaultButton);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons, System.Windows.Forms.MessageBoxIcon icon)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons, icon);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption, System.Windows.Forms.MessageBoxButtons buttons)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption), buttons);
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text, System.String caption)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text), LanguageManager.Get(caption));
        public static System.Windows.Forms.DialogResult Show(System.Windows.Forms.IWin32Window owner, System.String text)
            => System.Windows.Forms.MessageBox.Show(owner, LanguageManager.GetMessage(text));
    }
}
