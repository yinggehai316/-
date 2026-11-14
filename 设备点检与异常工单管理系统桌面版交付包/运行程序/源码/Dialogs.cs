using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Inspection
{
    /// <summary>统一的界面样式与小工具，避免各窗口各写一套。</summary>
    static class Ui
    {
        public static readonly Font BaseFont = new Font("Microsoft YaHei UI", 9F);
        public static readonly Color Ink = Color.FromArgb(31, 56, 84);
        public static readonly Color Muted = Color.FromArgb(107, 124, 147);
        public static readonly Color Accent = Color.FromArgb(31, 95, 168);
        public static readonly Color Line = Color.FromArgb(214, 224, 235);
        public static readonly Color PageBack = Color.FromArgb(244, 247, 251);
        public static readonly Color SoftBack = Color.FromArgb(242, 246, 250);

        public static Label Lbl(string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = Ink;
            label.Margin = new Padding(0, 9, 8, 0);
            return label;
        }

        public static Label Hint(string text)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = Muted;
            label.Margin = new Padding(0, 10, 8, 0);
            return label;
        }

        public static TextBox Txt(string value, int width)
        {
            var box = new TextBox();
            box.Text = value;
            box.Width = width;
            box.Margin = new Padding(0, 5, 10, 0);
            return box;
        }

        public static ComboBox Combo(string[] items)
        {
            var combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Items.AddRange(items);
            if (items.Length > 0) combo.SelectedIndex = 0;
            combo.Width = 104;
            combo.Margin = new Padding(0, 5, 10, 0);
            return combo;
        }

        public static Button Plain(string text)
        {
            var button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 30;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Line;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Color.White;
            button.ForeColor = Ink;
            button.Padding = new Padding(10, 0, 10, 0);
            button.Margin = new Padding(0, 0, 8, 0);
            button.Cursor = Cursors.Hand;
            return button;
        }

        public static Button Primary(string text)
        {
            var button = Plain(text);
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Accent;
            return button;
        }

        public static void Error(IWin32Window owner, string message)
        {
            MessageBox.Show(owner, message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Info(IWin32Window owner, string message)
        {
            MessageBox.Show(owner, message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static bool Confirm(IWin32Window owner, string message)
        {
            return MessageBox.Show(owner, message, "请确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }
    }

    sealed class EquipmentDialog : Form
    {
        readonly EquipmentRow row;
        readonly TextBox code;
        readonly TextBox name;
        readonly TextBox model;
        readonly TextBox location;
        readonly TextBox commissioned;
        readonly TextBox remark;
        readonly ComboBox status;

        public EquipmentDialog(EquipmentRow existing)
        {
            row = existing == null ? new EquipmentRow() : existing;

            Text = existing == null ? "新增设备" : "编辑设备";
            ClientSize = new Size(470, 388);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = Ui.BaseFont;
            BackColor = Color.White;
            Padding = new Padding(18);

            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 2;
            table.RowCount = 8;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            code = Ui.Txt(row.Code, 300);
            name = Ui.Txt(row.Name, 300);
            model = Ui.Txt(row.Model, 300);
            location = Ui.Txt(row.Location, 300);
            commissioned = Ui.Txt(row.CommissionedAt, 160);
            status = Ui.Combo(Store.EquipmentStatuses);
            status.Width = 120;
            if (existing != null) status.SelectedItem = row.Status;
            remark = Ui.Txt(row.Remark, 300);
            remark.Multiline = true;
            remark.Height = 64;
            remark.ScrollBars = ScrollBars.Vertical;

            AddRow(table, 0, "设备编号 *", code);
            AddRow(table, 1, "设备名称 *", name);
            AddRow(table, 2, "规格型号", model);
            AddRow(table, 3, "安装位置", location);
            AddRow(table, 4, "状态", status);
            AddRow(table, 5, "投用日期", commissioned);
            AddRow(table, 6, "备注", remark);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(0, 10, 0, 0);
            var save = Ui.Primary("保存");
            save.Click += SaveClick;
            var cancel = Ui.Plain("取消");
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            table.Controls.Add(buttons, 0, 7);
            table.SetColumnSpan(buttons, 2);

            Controls.Add(table);
            AcceptButton = save;
            CancelButton = cancel;
        }

        static void AddRow(TableLayoutPanel table, int rowIndex, string caption, Control control)
        {
            var label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.ForeColor = Ui.Muted;
            label.Anchor = AnchorStyles.Left;
            table.Controls.Add(label, 0, rowIndex);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            table.Controls.Add(control, 1, rowIndex);
        }

        void SaveClick(object sender, EventArgs e)
        {
            row.Code = code.Text.Trim();
            row.Name = name.Text.Trim();
            row.Model = model.Text.Trim();
            row.Location = location.Text.Trim();
            row.Status = Convert.ToString(status.SelectedItem);
            row.CommissionedAt = commissioned.Text.Trim();
            row.Remark = remark.Text.Trim();

            if (row.CommissionedAt.Length > 0)
            {
                DateTime parsed;
                if (!DateTime.TryParseExact(row.CommissionedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                {
                    Ui.Error(this, "投用日期格式应为 yyyy-MM-dd，例如 2021-03-15。");
                    return;
                }
            }

            try
            {
                Store.SaveEquipment(row);
                DialogResult = DialogResult.OK;
            }
            catch (DbException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    Ui.Error(this, "设备编号“" + row.Code + "”已存在，请换一个编号。");
                else
                    Ui.Error(this, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }
    }

    sealed class ItemDialog : Form
    {
        readonly CheckItemRow item;
        readonly TextBox name;
        readonly TextBox part;
        readonly TextBox unit;
        readonly TextBox lower;
        readonly TextBox upper;
        readonly ComboBox method;
        readonly CheckBox enabled;

        public ItemDialog(long equipmentId, CheckItemRow existing)
        {
            item = existing == null ? new CheckItemRow() : existing;
            item.EquipmentId = equipmentId;

            Text = existing == null ? "新增点检项目" : "编辑点检项目";
            ClientSize = new Size(450, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = Ui.BaseFont;
            BackColor = Color.White;
            Padding = new Padding(18);

            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 2;
            table.RowCount = 8;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            name = Ui.Txt(item.Name, 280);
            part = Ui.Txt(item.Part, 280);
            unit = Ui.Txt(item.Unit, 120);
            lower = Ui.Txt(existing == null ? "" : item.Lower.ToString("0.###", CultureInfo.InvariantCulture), 120);
            upper = Ui.Txt(existing == null ? "" : item.Upper.ToString("0.###", CultureInfo.InvariantCulture), 120);
            method = Ui.Combo(Store.Methods);
            if (existing != null) method.SelectedItem = item.Method;
            enabled = new CheckBox();
            enabled.Text = "启用（参与每日点检计划）";
            enabled.Checked = existing == null || item.Enabled;
            enabled.AutoSize = true;
            enabled.ForeColor = Ui.Ink;
            enabled.Margin = new Padding(0, 8, 0, 0);

            AddRow(table, 0, "项目名称 *", name);
            AddRow(table, 1, "点检部位", part);
            AddRow(table, 2, "计量单位", unit);
            AddRow(table, 3, "下限", lower);
            AddRow(table, 4, "上限", upper);
            AddRow(table, 5, "点检方法", method);
            table.Controls.Add(enabled, 1, 6);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(0, 10, 0, 0);
            var save = Ui.Primary("保存");
            save.Click += SaveClick;
            var cancel = Ui.Plain("取消");
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            table.Controls.Add(buttons, 0, 7);
            table.SetColumnSpan(buttons, 2);

            Controls.Add(table);
            AcceptButton = save;
            CancelButton = cancel;
        }

        static void AddRow(TableLayoutPanel table, int rowIndex, string caption, Control control)
        {
            var label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.ForeColor = Ui.Muted;
            label.Anchor = AnchorStyles.Left;
            table.Controls.Add(label, 0, rowIndex);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            table.Controls.Add(control, 1, rowIndex);
        }

        void SaveClick(object sender, EventArgs e)
        {
            item.Name = name.Text.Trim();
            item.Part = part.Text.Trim();
            item.Unit = unit.Text.Trim();
            item.Method = Convert.ToString(method.SelectedItem);
            item.Enabled = enabled.Checked;

            double low, high;
            if (!double.TryParse(lower.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out low))
            {
                Ui.Error(this, "下限必须填写数字。");
                return;
            }
            if (!double.TryParse(upper.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out high))
            {
                Ui.Error(this, "上限必须填写数字。");
                return;
            }
            if (low > high)
            {
                Ui.Error(this, "下限不能大于上限。");
                return;
            }
            item.Lower = low;
            item.Upper = high;

            try
            {
                Store.SaveItem(item);
                DialogResult = DialogResult.OK;
            }
            catch (DbException ex)
            {
                if (ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
                    Ui.Error(this, "该设备下已存在同名点检项目。");
                else
                    Ui.Error(this, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }
    }

    sealed class PromptDialog : Form
    {
        readonly TextBox box;
        readonly bool required;

        public string Value { get { return box.Text.Trim(); } }

        public PromptDialog(string title, string caption, string initial, bool multiline, bool isRequired)
        {
            required = isRequired;
            Text = title;
            ClientSize = new Size(440, multiline ? 250 : 158);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = Ui.BaseFont;
            BackColor = Color.White;
            Padding = new Padding(16);

            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 3;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            var label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.ForeColor = Ui.Ink;

            box = new TextBox();
            box.Text = initial;
            box.Dock = DockStyle.Fill;
            box.Multiline = multiline;
            if (multiline) box.ScrollBars = ScrollBars.Vertical;

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            var ok = Ui.Primary("确定");
            ok.Click += OkClick;
            var cancel = Ui.Plain("取消");
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            table.Controls.Add(label, 0, 0);
            table.Controls.Add(box, 0, 1);
            table.Controls.Add(buttons, 0, 2);
            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        void OkClick(object sender, EventArgs e)
        {
            if (required && Value.Length == 0)
            {
                Ui.Error(this, "该项不能为空。");
                return;
            }
            DialogResult = DialogResult.OK;
        }
    }

    static class Prompt
    {
        public static string Ask(IWin32Window owner, string title, string caption, string initial, bool multiline, bool required)
        {
            using (var dialog = new PromptDialog(title, caption, initial, multiline, required))
            {
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Value : null;
            }
        }
    }
}