using System;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class LazyTabPage : TabPage
    {
        private readonly Func<Control> _factory;
        private bool _built;

        public LazyTabPage(string text, Func<Control> factory)
            : base(text)
        {
            _factory = factory;
            BackColor = DS.BgPage;
        }

        public bool IsBuilt => _built;

        public void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            Control content = _factory == null ? null : _factory();
            if (content == null)
                return;

            content.Dock = DockStyle.Fill;
            Controls.Add(content);
        }
    }
}
