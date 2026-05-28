using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

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
            AssertActionVariant("Save Draft", UiActionVariant.Success);
            AssertActionVariant("Upload PDF", UiActionVariant.Secondary);
            AssertActionVariant("Delete", UiActionVariant.Danger);
            AssertActionVariant("Send for Approval", UiActionVariant.Primary);
            AssertActionVariant("Generate PDF", UiActionVariant.Primary);
            AssertActionVariant("Convert to Purchase Order", UiActionVariant.Secondary);
            AssertActionVariant("Convert to Invoice", UiActionVariant.Secondary);
            AssertActionVariant("Create Dispatch Job", UiActionVariant.Primary);
            AssertActionVariant("WhatsApp Follow-up", UiActionVariant.Secondary);
            AssertActionVariant("Clear Form", UiActionVariant.Secondary);
            AssertActionVariant("Resolve", UiActionVariant.Success);
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
            }
        }

    }
}
