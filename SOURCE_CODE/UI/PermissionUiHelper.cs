using System;
using System.Collections.Generic;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal static class PermissionUiHelper
    {
        public static void EnsureSession(Control host)
        {
            if (SessionManager.IsLoggedIn || host == null || host.IsDisposed)
                return;

            Action closeHost = () =>
            {
                Form form = host.FindForm();
                form?.Close();
            };

            if (host.IsHandleCreated)
            {
                host.BeginInvoke(closeHost);
                return;
            }

            EventHandler handler = null;
            handler = (s, e) =>
            {
                host.HandleCreated -= handler;
                if (!host.IsDisposed)
                    host.BeginInvoke(closeHost);
            };
            host.HandleCreated += handler;
        }

        public static void ApplyModulePermissions(string moduleKey, Control editScope, Control createButton, Control editButton, Control deleteButton)
        {
            EnsureSession(editScope ?? createButton ?? editButton ?? deleteButton);

            bool canCreate = SessionManager.HasPermission(moduleKey, "Create");
            bool canEdit = SessionManager.HasPermission(moduleKey, "Edit");
            bool canDelete = SessionManager.HasPermission(moduleKey, "Delete");

            if (createButton != null)
                createButton.Visible = canCreate;
            if (editButton != null)
                editButton.Visible = canEdit;
            if (deleteButton != null)
                deleteButton.Visible = canDelete;

            if (!canEdit && editScope != null)
                SetReadOnly(editScope, new HashSet<Control>(new[] { createButton, editButton, deleteButton }));
        }

        private static void SetReadOnly(Control parent, HashSet<Control> excluded)
        {
            foreach (Control child in parent.Controls)
            {
                if (child == null || excluded.Contains(child))
                    continue;

                if (child is TextBox tb)
                {
                    tb.ReadOnly = true;
                }
                else if (child is ComboBox combo)
                {
                    combo.Enabled = false;
                }
                else if (child is DateTimePicker dtp)
                {
                    dtp.Enabled = false;
                }
                else if (child is NumericUpDown nud)
                {
                    nud.Enabled = false;
                }
                else if (child is CheckBox chk)
                {
                    chk.Enabled = false;
                }
                else if (child is RadioButton radio)
                {
                    radio.Enabled = false;
                }
                else if (child is DataGridView grid)
                {
                    grid.ReadOnly = true;
                    grid.AllowUserToAddRows = false;
                    grid.AllowUserToDeleteRows = false;
                }

                if (child.HasChildren)
                    SetReadOnly(child, excluded);
            }
        }
    }
}
