using System;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class LazyDetailHost : Panel
    {
        private Func<Control> _factory;
        private Control _content;

        public LazyDetailHost()
        {
            Dock = DockStyle.Fill;
        }

        public bool IsCreated => _content != null && !_content.IsDisposed;

        public Control Content => _content;

        public void SetFactory(Func<Control> factory)
        {
            _factory = factory;
        }

        public Control EnsureContentCreated()
        {
            if (_content != null && !_content.IsDisposed)
                return _content;

            if (_factory == null)
                return null;

            _content = _factory();
            if (_content == null)
                return null;

            _content.Dock = DockStyle.Fill;
            Controls.Add(_content);
            return _content;
        }

        public void ShowContent()
        {
            Control content = EnsureContentCreated();
            if (content != null)
            {
                content.Visible = true;
                content.BringToFront();
            }
        }
    }
}
