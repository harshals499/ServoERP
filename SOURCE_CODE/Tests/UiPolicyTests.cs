using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.UI.Controls;

namespace HVAC_Pro_Desktop.Tests
{
    public static class UiPolicyTests
    {
        public static List<string> RunAll()
        {
            EnsureGridColumnPolicyHonorsMinimumWidth();
            EnsureGridColumnPolicySurvivesGridThemeLifecycleHandlers();
            EnsureActionStyleResolverMapsCoreLabels();
            EnsureActionButtonAppliesSecondaryBorder();
            EnsureSoftBorderDesignTokens();
            EnsureDispatchTechnicianClassification();
            EnsureGlobalPaginationControlNavigatesSafely();
            EnsureGlobalPaginationControlKeepsControlsInsideBounds();
            EnsureGlobalButtonStylingPreservesBusinessTags();
            EnsureSidebarNavigationSurvivesGlobalButtonStyling();
            EnsureLockedCardKeepsUserSizeDuringPacking();
            EnsureAllLoggedInUsersHaveFullRoleAccess();
            EnsureForgotPasswordUsesSelfServiceDialog();
            return new List<string> { "PASS UI policies verified" };
        }

        private static void EnsureGridColumnPolicyHonorsMinimumWidth()
        {
            using (var grid = new DataGridView())
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total (INR)", Width = 20 });
                GridTheme.Apply(grid);
                GridTheme.ApplyColumnPolicy(grid, new[] { new GridColumnPolicy("Total", 120, GridColumnPriority.Required) });

                if (grid.Columns["Total"].Width < 120)
                    throw new InvalidOperationException("Grid policy did not honor required minimum width.");
            }
        }

        private static void EnsureGridColumnPolicySurvivesGridThemeLifecycleHandlers()
        {
            using (var grid = new DataGridView())
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total", Width = 20 });
                GridTheme.Apply(grid);
                GridTheme.ApplyColumnPolicy(grid, new[]
                {
                    new GridColumnPolicy("Total", 140, GridColumnPriority.Required),
                    new GridColumnPolicy("LateTotal", 160, GridColumnPriority.Required)
                });

                InvokeDataBindingComplete(grid);
                AssertFixedPolicyColumn(grid.Columns["Total"], 140, "DataBindingComplete");

                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LateTotal", HeaderText = "Late Total", Width = 20 });
                AssertFixedPolicyColumn(grid.Columns["LateTotal"], 160, "ColumnAdded");
                AssertFixedPolicyColumn(grid.Columns["Total"], 140, "ColumnAdded existing column");
            }
        }

        private static void InvokeDataBindingComplete(DataGridView grid)
        {
            MethodInfo method = typeof(DataGridView).GetMethod(
                "OnDataBindingComplete",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(DataGridViewBindingCompleteEventArgs) },
                null);
            method.Invoke(grid, new object[] { new DataGridViewBindingCompleteEventArgs(ListChangedType.Reset) });
        }

        private static void AssertFixedPolicyColumn(DataGridViewColumn column, int minimumWidth, string lifecycle)
        {
            if (column.AutoSizeMode != DataGridViewAutoSizeColumnMode.None)
                throw new InvalidOperationException("Grid policy was reset to " + column.AutoSizeMode + " after " + lifecycle + ".");

            if (column.MinimumWidth < minimumWidth || column.Width < minimumWidth)
                throw new InvalidOperationException("Grid policy width was not preserved after " + lifecycle + ".");
        }

        private static void EnsureActionStyleResolverMapsCoreLabels()
        {
            AssertActionVariant("Save Draft", UiActionVariant.Primary);
            AssertActionVariant("Upload PDF", UiActionVariant.Secondary);
            AssertActionVariant("Delete", UiActionVariant.Danger);
            AssertActionVariant("Send for Approval", UiActionVariant.Primary);
            AssertActionVariant("Generate PDF", UiActionVariant.Primary);
            AssertActionVariant("Convert to Purchase Order", UiActionVariant.Secondary);
            AssertActionVariant("Convert to Invoice", UiActionVariant.Secondary);
            AssertActionVariant("Create Dispatch Job", UiActionVariant.Primary);
            AssertActionVariant("WhatsApp Follow-up", UiActionVariant.Secondary);
            AssertActionVariant("Clear Form", UiActionVariant.Danger);
            AssertActionVariant("Resolve", UiActionVariant.Primary);
        }

        private static void AssertActionVariant(string label, UiActionVariant expected)
        {
            UiActionVariant actual = UIHelper.ResolveActionVariant(label);
            if (actual != expected)
                throw new InvalidOperationException(label + " must resolve to " + expected + " but resolved to " + actual + ".");
        }

        private static void EnsureActionButtonAppliesSecondaryBorder()
        {
            using (var button = new Button { Text = "Upload PDF" })
            {
                UIHelper.ApplyActionButton(button);
                if (button.FlatAppearance.BorderSize < 1)
                    throw new InvalidOperationException("Secondary action buttons must have a visible border.");
                if (button.FlatAppearance.BorderColor.ToArgb() != DS.InputBorder.ToArgb())
                    throw new InvalidOperationException("Secondary action button border must use the soft shared border token.");
            }
        }

        private static void EnsureSoftBorderDesignTokens()
        {
            Color expected = Color.FromArgb(209, 213, 219);
            if (DS.Border.ToArgb() != expected.ToArgb())
                throw new InvalidOperationException("DS.Border must remain #D1D5DB for app-wide outline consistency.");
            if (DS.BorderStrong.ToArgb() != expected.ToArgb())
                throw new InvalidOperationException("DS.BorderStrong must remain #D1D5DB for app-wide outline consistency.");
            if (GridTheme.BorderColor.ToArgb() != expected.ToArgb())
                throw new InvalidOperationException("Grid outer border token must remain #D1D5DB.");
            if (GridTheme.GridLine.ToArgb() != expected.ToArgb())
                throw new InvalidOperationException("Grid line token must remain #D1D5DB.");
        }

        private static void EnsureDispatchTechnicianClassification()
        {
            if (!EmployeeService.IsDispatchTechnicianRole(new Employee { Designation = "AC Technician", Department = "Semi Skilled", NatureOfWork = "Semi Skilled" }))
                throw new InvalidOperationException("AC Technician must be visible in Dispatch Center.");
            if (!EmployeeService.IsDispatchTechnicianRole(new Employee { Designation = "Helper", Department = "Semi Skilled", NatureOfWork = "Semi Skilled" }))
                throw new InvalidOperationException("Helper field staff must be visible in Dispatch Center.");
            if (!EmployeeService.IsDispatchTechnicianRole(new Employee { Designation = "DCS OFFICER", Department = "Skilled", NatureOfWork = "Skilled" }))
                throw new InvalidOperationException("DCS Officer field staff must be visible in Dispatch Center.");
            if (EmployeeService.IsDispatchTechnicianRole(new Employee { Designation = "Sr.Accountant", Department = "Skilled", NatureOfWork = "Skilled" }))
                throw new InvalidOperationException("Accounts staff must not be counted as dispatch technicians.");
            if (EmployeeService.GetDispatchTechnicianSortRank(new Employee { Designation = "HVAC Technician" }) >= EmployeeService.GetDispatchTechnicianSortRank(new Employee { Designation = "Helper" }))
                throw new InvalidOperationException("Core technician roles must sort before helper roles.");
        }

        private static void EnsureGlobalPaginationControlNavigatesSafely()
        {
            using (var pager = new GlobalPaginationControl())
            {
                int pageChanged = 0;
                int pageSizeChanged = 0;
                pager.PageChanged += (s, e) => pageChanged++;
                pager.PageSizeChanged += (s, e) => pageSizeChanged++;

                pager.SetState(1, 125, 10);
                if (pager.CurrentPage != 1 || pager.TotalPages != 13 || pager.DisplayFrom != 1 || pager.DisplayTo != 10)
                    throw new InvalidOperationException("Pagination state did not initialise correctly.");

                pager.GoToPage(5);
                if (pager.CurrentPage != 5 || pageChanged != 1 || pager.DisplayFrom != 41 || pager.DisplayTo != 50)
                    throw new InvalidOperationException("Direct page navigation did not update the current slice.");

                pager.GoToPage(99);
                if (pager.CurrentPage != 13 || pager.DisplayTo != 125)
                    throw new InvalidOperationException("Pagination did not clamp overflow page numbers.");

                pager.GoToPage(-3);
                if (pager.CurrentPage != 1)
                    throw new InvalidOperationException("Pagination did not clamp negative page numbers.");

                pager.SetState(1, 0, 25);
                if (pager.CurrentPage != 1 || pager.TotalPages != 0 || pager.DisplayFrom != 0 || pager.DisplayTo != 0)
                    throw new InvalidOperationException("Pagination empty state is invalid.");

                if (pageSizeChanged != 0)
                    throw new InvalidOperationException("Programmatic pagination state changes must not fire page size events.");
            }
        }

        private static void EnsureGlobalPaginationControlKeepsControlsInsideBounds()
        {
            foreach (int width in new[] { 600, 420, 300 })
            {
                using (var pager = new GlobalPaginationControl())
                {
                    pager.Size = new Size(width, 38);
                    pager.SetState(1, 397, 10);

                    foreach (Control child in pager.Controls)
                    {
                        if (!child.Visible)
                            continue;

                        if (child.Left < 0 || child.Top < 0 || child.Right > pager.ClientSize.Width || child.Bottom > pager.ClientSize.Height)
                            throw new InvalidOperationException("Pagination child control is outside bounds at width " + width + ": " + child.GetType().Name + " " + child.Bounds + " within " + pager.ClientSize + ".");
                    }
                }
            }
        }

        private static void EnsureSidebarNavigationSurvivesGlobalButtonStyling()
        {
            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiresAt = SessionManager.ExpiresAt;
            try
            {
                SessionManager.SetSession(new AppUserDto
                {
                    UserId = 999997,
                    Username = "ui.policy",
                    DisplayName = "UI Policy",
                    RoleName = "Administrator",
                    IsActive = true,
                    Permissions = new Dictionary<string, RolePermissionDto>(StringComparer.OrdinalIgnoreCase)
                });

                using (var form = new MainForm())
                {
                    UIHelper.ApplyButtonAlignment(form);

                    int navButtons = 0;
                    foreach (Button button in EnumerateControls(form).OfType<Button>())
                    {
                        if (string.IsNullOrWhiteSpace(button.AccessibleName))
                            continue;

                        if (button.GetType().Name != "SidebarNavButton")
                            continue;

                        navButtons++;
                        if (!(button.Tag is int))
                            throw new InvalidOperationException("Global button styling must not overwrite sidebar navigation Tag values.");
                    }

                    if (navButtons == 0)
                        throw new InvalidOperationException("Sidebar navigation buttons were not created for policy validation.");
                }
            }
            finally
            {
                SessionManager.SetSession(previousUser, previousSessionId, previousExpiresAt);
            }
        }

        private static void EnsureGlobalButtonStylingPreservesBusinessTags()
        {
            using (var flow = new FlowLayoutPanel())
            using (var button = new Button { Text = "Revenue", Tag = 7, Width = 160, Height = 38 })
            {
                flow.Controls.Add(button);
                UIHelper.ApplyButtonAlignment(flow);

                if (!(button.Tag is int) || (int)button.Tag != 7)
                    throw new InvalidOperationException("Global button styling must not overwrite business/navigation Button.Tag values.");
            }
        }

        private static void EnsureLockedCardKeepsUserSizeDuringPacking()
        {
            using (var page = new LockedCardPolicyPage())
            {
                var flow = new FlowLayoutPanel
                {
                    Name = "LockedCardPolicyFlow",
                    Size = new Size(900, 260),
                    WrapContents = true,
                    AutoScroll = true,
                    BackColor = DS.BgPage
                };

                Panel lockedCard = MakePolicyCard("LockedCard", 260, 120);
                Panel fillerCard = MakePolicyCard("FillerCard", 260, 120);
                flow.Controls.Add(lockedCard);
                flow.Controls.Add(fillerCard);
                page.Controls.Add(flow);

                GlobalDashboardLayoutService.ApplyToTree(page);
                lockedCard.Size = new Size(310, 150);
                GlobalDashboardLayoutService.SetCardLocked(lockedCard, true);
                Size lockedSize = lockedCard.Size;

                GlobalDashboardLayoutService.ApplyToTree(page);

                if (lockedCard.Size != lockedSize)
                    throw new InvalidOperationException("Locked cards must retain the exact user-set width and height during automatic card packing.");
                if (!GlobalDashboardLayoutService.IsCardLocked(lockedCard))
                    throw new InvalidOperationException("Locked card state was not retained after global layout reapply.");

                Control lockBadge = lockedCard.Controls.Cast<Control>().FirstOrDefault(control => control.Name == CardResizeGripService.LockBadgeName);
                if (lockBadge == null || !lockBadge.Visible)
                    throw new InvalidOperationException("Locked cards must show a visible Locked badge after locking.");
            }
        }

        private static Panel MakePolicyCard(string name, int width, int height)
        {
            var card = new Panel
            {
                Name = name,
                Tag = "dashboard-card",
                Size = new Size(width, height),
                MinimumSize = new Size(180, 96),
                BackColor = Color.White,
                Margin = new Padding(0, 0, 12, 12),
                Padding = new Padding(12)
            };
            card.Controls.Add(new Label
            {
                Text = name,
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });
            return card;
        }

        private sealed class LockedCardPolicyPage : UserControl
        {
            public LockedCardPolicyPage()
            {
                Name = "LockedCardPolicyPage";
                Size = new Size(920, 300);
                BackColor = DS.BgPage;
            }
        }

        private static void EnsureAllLoggedInUsersHaveFullRoleAccess()
        {
            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiresAt = SessionManager.ExpiresAt;
            try
            {
                SessionManager.SetSession(new AppUserDto
                {
                    UserId = 999998,
                    Username = "legacy.viewer",
                    DisplayName = "Legacy Viewer",
                    RoleName = "Viewer",
                    IsActive = true,
                    Permissions = new Dictionary<string, RolePermissionDto>(StringComparer.OrdinalIgnoreCase)
                });

                if (!SessionManager.HasPermission("Settings", "View")
                    || !SessionManager.HasPermission("Settings", "Create")
                    || !SessionManager.HasPermission("Settings", "Edit")
                    || !SessionManager.HasPermission("Settings", "Delete"))
                {
                    throw new InvalidOperationException("All logged-in users must receive full role access inside licensed modules.");
                }
            }
            finally
            {
                SessionManager.SetSession(previousUser, previousSessionId, previousExpiresAt);
            }
        }

        private static void EnsureForgotPasswordUsesSelfServiceDialog()
        {
            using (var dialog = new ChangePasswordForm("operator@example.com"))
            {
                if (!ContainsLabel(dialog, "Username / Email"))
                    throw new InvalidOperationException("Forgot Password must ask for username or email, not the current password.");

                if (ContainsLabel(dialog, "Current Password"))
                    throw new InvalidOperationException("Forgot Password must not require the old password or administrator reset.");

                if (!ContainsButton(dialog, "Reset Password"))
                    throw new InvalidOperationException("Forgot Password must show a direct Reset Password action.");
            }
        }

        private static bool ContainsLabel(Control root, string text)
        {
            foreach (Control control in EnumerateControls(root))
            {
                Label label = control as Label;
                if (label != null && string.Equals((label.Text ?? string.Empty).Trim(), text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsButton(Control root, string text)
        {
            foreach (Control control in EnumerateControls(root))
            {
                Button button = control as Button;
                if (button != null && string.Equals((button.Text ?? string.Empty).Trim(), text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            if (root == null)
                yield break;

            yield return root;
            foreach (Control child in root.Controls)
            {
                foreach (Control nested in EnumerateControls(child))
                    yield return nested;
            }
        }

    }
}
