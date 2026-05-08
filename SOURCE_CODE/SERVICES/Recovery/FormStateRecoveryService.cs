using System;
using System.IO;
using System.Web.Script.Serialization;

namespace HVAC_Pro_Desktop.Services.Recovery
{
    public sealed class FormStateRecoveryService
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "FormRecovery");

        public void SaveState<T>(string formKey, T state)
        {
            if (string.IsNullOrWhiteSpace(formKey) || state == null)
                return;
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, MakeSafe(formKey) + ".json"), _serializer.Serialize(state));
        }

        public bool TryLoadState<T>(string formKey, out T state)
        {
            state = default(T);
            string path = Path.Combine(Root, MakeSafe(formKey) + ".json");
            if (!File.Exists(path))
                return false;
            state = _serializer.Deserialize<T>(File.ReadAllText(path));
            return true;
        }

        public void ClearState(string formKey)
        {
            string path = Path.Combine(Root, MakeSafe(formKey) + ".json");
            if (File.Exists(path))
                File.Delete(path);
        }

        private static string MakeSafe(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }
    }
}
