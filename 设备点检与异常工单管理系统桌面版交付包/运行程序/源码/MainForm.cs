using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Inspection
{
    sealed class MainForm : Form
    {
        TabControl tabs;
        ToolStripStatusLabel statusDb;
        ToolStripStatusLabel statusRight;

        // 设备台账
        TextBox eqSearch;
        ComboBox eqStatus;
        DataGridView eqGrid;

        // 点检标准
        ListBox itemList;
        Label itemTitle;
        DataGridView itemGrid;

        // 点检任务
        DateTimePicker planDate;
        ComboBox planStatus;
        ComboBox operatorBox;
        DataGridView planGrid;
        DataGridView recordGrid;
        Label recordTitle;
        bool fillingPlans;
        const int ValueColumn = 6;
        const int JudgeColumn = 7;
        const int NoteColumn = 8;

        // 维修工单
        ComboBox orderStatus;
        DataGridView orderGrid;

        // 统计报表
        DateTimePicker statFrom;
        DateTimePicker statTo;
        Label[] kpiValues;
        DataGridView statGrid;
        DataGridView abnormalGrid;

        // 操作日志
        DataGridView logGrid;

        // 分栏（尺寸在 OnShown 中设置，避免构造期被默认 150px 宽度钳制）
        SplitContainer itemSplit;
        SplitContainer taskSplit;
        SplitContainer statSplit;

        public MainForm()
        {
            Text = "设备点检与异常工单管理系统";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(1020, 680);
            Font = Ui.BaseFont;
            BackColor = Ui.PageBack;

            var menu = BuildMenu();
            var root = BuildHeaderAndTabs();
            var strip = BuildStatusBar();

            Controls.Add(root);
            Controls.Add(menu);
            Controls.Add(strip);
            MainMenuStrip = menu;

            FillOperators();
            LoadEquipments();
            LoadEquipmentList();
            LoadPlans();
            LoadOrders();
            LoadLogs();
            RefreshStats();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (itemSplit != null && itemSplit.Width > 400) itemSplit.SplitterDistance = 250;
            if (taskSplit != null && taskSplit.Height > 400) taskSplit.SplitterDistance = 250;
            if (statSplit != null && statSplit.Height > 400) statSplit.SplitterDistance = 230;
        }

        // ================= 框架 =================

        MenuStrip BuildMenu()
        {
            var menu = new MenuStrip();
            menu.BackColor = Color.White;
            menu.Font = Ui.BaseFont;
            menu.Padding = new Padding(8, 4, 0, 4);

            var file = new ToolStripMenuItem("文件(&F)");
            file.DropDownItems.Add(Item("备份数据库…", delegate { BackupDatabase(); }));
            file.DropDownItems.Add(Item("打开数据目录", delegate { OpenDataFolder(); }));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Item("退出", delegate { Close(); }));

            var data = new ToolStripMenuItem("数据(&D)");
            data.DropDownItems.Add(Item("生成今日点检计划", delegate
            {
                int created = Store.GeneratePlans(DateTime.Today);
                LoadPlans();
                LoadLogs();
                Ui.Info(this, created > 0 ? "已生成 " + created + " 张点检计划。" : "今日计划已存在，无需重复生成。");
            }));
            data.DropDownItems.Add(Item("重置示例数据", delegate { ResetDatabase(); }));

            var help = new ToolStripMenuItem("帮助(&H)");
            help.DropDownItems.Add(Item("关于", delegate { ShowAbout(); }));

            menu.Items.Add(file);
            menu.Items.Add(data);
            menu.Items.Add(help);
            return menu;
        }

        static ToolStripMenuItem Item(string text, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += handler;
            return item;
        }

        TableLayoutPanel BuildHeaderAndTabs()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.White;
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Ui.Line))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var badge = new Label();
            badge.Dock = DockStyle.Right;
            badge.Width = 320;
            badge.Text = "SQLite 单机版  ·  工业软件基础实践";
            badge.TextAlign = ContentAlignment.MiddleRight;
            badge.ForeColor = Ui.Muted;
            badge.Padding = new Padding(0, 0, 26, 0);

            var accent = new Panel();
            accent.BackColor = Ui.Accent;
            accent.Location = new Point(26, 20);
            accent.Size = new Size(4, 38);

            var title = new Label();
            title.Text = "设备点检与异常工单管理系统";
            title.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            title.ForeColor = Ui.Ink;
            title.AutoSize = true;
            title.Location = new Point(40, 13);

            var subtitle = new Label();
            subtitle.Text = "设备台账 · 点检标准 · 每日计划 · 异常判定 · 维修工单 · 统计追溯";
            subtitle.ForeColor = Ui.Muted;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(42, 47);

            header.Controls.Add(badge);
            header.Controls.Add(accent);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = new Font("Microsoft YaHei UI", 9.5F);
            tabs.Padding = new Point(14, 6);
            tabs.TabPages.Add(BuildEquipmentPage());
            tabs.TabPages.Add(BuildItemPage());
            tabs.TabPages.Add(BuildTaskPage());
            tabs.TabPages.Add(BuildOrderPage());
            tabs.TabPages.Add(BuildStatPage());
            tabs.TabPages.Add(BuildLogPage());

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(tabs, 0, 1);
            return root;
        }

        StatusStrip BuildStatusBar()
        {
            var strip = new StatusStrip();
            strip.BackColor = Color.White;
            strip.SizingGrip = false;

            statusDb = new ToolStripStatusLabel("数据库：" + Store.DatabasePath);
            statusDb.ForeColor = Ui.Muted;
            statusDb.Spring = false;

            statusRight = new ToolStripStatusLabel("就绪");
            statusRight.Spring = true;
            statusRight.TextAlign = ContentAlignment.MiddleRight;
            statusRight.ForeColor = Ui.Muted;

            strip.Items.Add(statusDb);
            strip.Items.Add(statusRight);
            return strip;
        }

        static TabPage Page(string text)
        {
            var page = new TabPage(text);
            page.BackColor = Color.White;
            page.Padding = new Padding(14, 12, 14, 12);
            return page;
        }

        static FlowLayoutPanel Bar()
        {
            var bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.AutoSize = true;
            bar.WrapContents = false;
            bar.BackColor = Color.White;
            bar.Padding = new Padding(0, 4, 0, 4);
            return bar;
        }

        static TableLayoutPanel Stack()
        {
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 2;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return table;
        }

        // ================= 设备台账 =================

        TabPage BuildEquipmentPage()
        {
            var page = Page("设备台账");
            var table = Stack();
            var bar = Bar();

            eqSearch = Ui.Txt("", 170);
            eqStatus = Ui.Combo(new[] { "全部", "在用", "停用", "维修中" });
            eqSearch.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    LoadEquipments();
                    e.SuppressKeyPress = true;
                }
            };

            var query = Ui.Primary("查询");
            query.Click += delegate { LoadEquipments(); };
            var add = Ui.Plain("新增设备");
            add.Click += delegate { AddEquipment(); };
            var edit = Ui.Plain("编辑");
            edit.Click += delegate { EditEquipment(); };
            var toggle = Ui.Plain("停用 / 启用");
            toggle.Click += delegate { ToggleEquipment(); };
            var remove = Ui.Plain("删除");
            remove.Click += delegate { DeleteEquipment(); };
            var refresh = Ui.Plain("刷新");
            refresh.Click += delegate { LoadEquipments(); };

            bar.Controls.Add(Ui.Lbl("关键字"));
            bar.Controls.Add(eqSearch);
            bar.Controls.Add(Ui.Lbl("状态"));
            bar.Controls.Add(eqStatus);
            bar.Controls.Add(query);
            bar.Controls.Add(add);
            bar.Controls.Add(edit);
            bar.Controls.Add(toggle);
            bar.Controls.Add(remove);
            bar.Controls.Add(refresh);

            eqGrid = Grid();
            AddCol(eqGrid, "设备编号", 70);
            AddCol(eqGrid, "设备名称", 130);
            AddCol(eqGrid, "型号", 90);
            AddCol(eqGrid, "安装位置", 140);
            AddCol(eqGrid, "状态", 60);
            AddCol(eqGrid, "点检项", 60);
            AddCol(eqGrid, "未关闭工单", 90);
            AddCol(eqGrid, "最近点检", 90);
            eqGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0) EditEquipment();
            };

            table.Controls.Add(bar, 0, 0);
            table.Controls.Add(eqGrid, 0, 1);
            page.Controls.Add(table);
            return page;
        }

        void LoadEquipments()
        {
            string keyword = eqSearch == null ? "" : eqSearch.Text.Trim();
            string status = eqStatus == null ? "全部" : Convert.ToString(eqStatus.SelectedItem);
            var list = Store.GetEquipments(keyword, status);

            eqGrid.Rows.Clear();
            foreach (var e in list)
            {
                int index = eqGrid.Rows.Add(e.Code, e.Name, e.Model, e.Location, e.Status,
                    e.ItemCount, e.OpenOrders, e.LastCheck);
                var row = eqGrid.Rows[index];
                row.Tag = e;
                var statusCell = row.Cells[4];
                if (e.Status == Store.StatusInUse) statusCell.Style.ForeColor = Color.FromArgb(22, 120, 78);
                else if (e.Status == Store.StatusRepair) statusCell.Style.ForeColor = Color.FromArgb(180, 95, 6);
                else statusCell.Style.ForeColor = Ui.Muted;
                if (e.OpenOrders > 0)
                {
                    row.Cells[6].Style.ForeColor = Color.FromArgb(180, 60, 40);
                    row.Cells[6].Style.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                }
            }
            if (statusRight != null)
                statusRight.Text = "设备 " + list.Count + " 台（在用 " + Store.GetEquipments("", Store.StatusInUse).Count + " 台）";
        }

        EquipmentRow SelectedEquipment()
        {
            if (eqGrid.CurrentRow == null) return null;
            return eqGrid.CurrentRow.Tag as EquipmentRow;
        }

        void AddEquipment()
        {
            using (var dialog = new EquipmentDialog(null))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadEquipments();
                    LoadEquipmentList();
                    LoadLogs();
                }
            }
        }

        void EditEquipment()
        {
            var equipment = SelectedEquipment();
            if (equipment == null)
            {
                Ui.Info(this, "请先选择一行设备。");
                return;
            }
            using (var dialog = new EquipmentDialog(equipment))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadEquipments();
                    LoadEquipmentList();
                    LoadLogs();
                }
            }
        }

        void ToggleEquipment()
        {
            var equipment = SelectedEquipment();
            if (equipment == null)
            {
                Ui.Info(this, "请先选择一行设备。");
                return;
            }
            string target = equipment.Status == Store.StatusInUse ? Store.StatusStopped : Store.StatusInUse;
            if (target == Store.StatusStopped && !Ui.Confirm(this, "确定停用设备 " + equipment.Code + " 吗？停用后不再生成点检计划。"))
                return;
            try
            {
                Store.SetEquipmentStatus(equipment.Id, target);
                LoadEquipments();
                LoadEquipmentList();
                LoadLogs();
            }
            catch (DbException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        void DeleteEquipment()
        {
            var equipment = SelectedEquipment();
            if (equipment == null)
            {
                Ui.Info(this, "请先选择一行设备。");
                return;
            }
            if (!Ui.Confirm(this, "确定删除设备 " + equipment.Code + " " + equipment.Name + " 吗？")) return;
            try
            {
                if (Store.DeleteEquipment(equipment.Id))
                {
                    LoadEquipments();
                    LoadEquipmentList();
                    LoadLogs();
                }
                else
                {
                    Ui.Info(this, "该设备已有点检计划或工单记录，不能删除。可将其状态改为“停用”。");
                }
            }
            catch (DbException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        // ================= 点检标准 =================

        TabPage BuildItemPage()
        {
            var page = Page("点检标准");
            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            itemSplit = split;
            split.SplitterDistance = 250;
            split.SplitterWidth = 8;
            split.FixedPanel = FixedPanel.Panel1;

            var left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 2;
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var listCaption = Ui.Hint("设备列表");
            listCaption.Margin = new Padding(0, 4, 0, 6);
            itemList = new ListBox();
            itemList.Dock = DockStyle.Fill;
            itemList.BorderStyle = BorderStyle.None;
            itemList.IntegralHeight = false;
            itemList.BackColor = Ui.SoftBack;
            itemList.Font = new Font("Microsoft YaHei UI", 9.5F);
            itemList.ItemHeight = 30;
            itemList.SelectedIndexChanged += delegate { LoadItems(SelectedEquipmentId()); };
            left.Controls.Add(listCaption, 0, 0);
            left.Controls.Add(itemList, 0, 1);
            split.Panel1.Controls.Add(left);

            var right = Stack();
            var bar = Bar();
            itemTitle = new Label();
            itemTitle.Text = "点检项目";
            itemTitle.AutoSize = true;
            itemTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            itemTitle.ForeColor = Ui.Ink;
            itemTitle.Margin = new Padding(0, 7, 18, 0);

            var add = Ui.Primary("新增项目");
            add.Click += delegate { AddItem(); };
            var edit = Ui.Plain("编辑");
            edit.Click += delegate { EditItem(); };
            var remove = Ui.Plain("删除");
            remove.Click += delegate { DeleteItem(); };
            var toggle = Ui.Plain("启用 / 停用");
            toggle.Click += delegate { ToggleItem(); };
            var refresh = Ui.Plain("刷新");
            refresh.Click += delegate { LoadItems(SelectedEquipmentId()); };

            bar.Controls.Add(itemTitle);
            bar.Controls.Add(add);
            bar.Controls.Add(edit);
            bar.Controls.Add(remove);
            bar.Controls.Add(toggle);
            bar.Controls.Add(refresh);

            itemGrid = Grid();
            AddCol(itemGrid, "点检项目", 150);
            AddCol(itemGrid, "点检部位", 110);
            AddCol(itemGrid, "单位", 60);
            AddCol(itemGrid, "下限", 70);
            AddCol(itemGrid, "上限", 70);
            AddCol(itemGrid, "点检方法", 80);
            AddCol(itemGrid, "状态", 60);
            itemGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0) EditItem();
            };

            right.Controls.Add(bar, 0, 0);
            right.Controls.Add(itemGrid, 0, 1);
            split.Panel2.Controls.Add(right);

            page.Controls.Add(split);
            return page;
        }

        sealed class EqEntry
        {
            public readonly EquipmentRow Row;
            public EqEntry(EquipmentRow row) { Row = row; }
            public override string ToString()
            {
                string suffix = Row.Status == Store.StatusInUse ? "" : "（" + Row.Status + "）";
                return Row.Code + "   " + Row.Name + suffix;
            }
        }

        void LoadEquipmentList()
        {
            long keep = SelectedEquipmentId();
            itemList.BeginUpdate();
            itemList.Items.Clear();
            foreach (var e in Store.GetEquipments("", "全部")) itemList.Items.Add(new EqEntry(e));
            itemList.EndUpdate();

            if (itemList.Items.Count == 0) return;
            int index = 0;
            for (int i = 0; i < itemList.Items.Count; i++)
                if (((EqEntry)itemList.Items[i]).Row.Id == keep) index = i;
            itemList.SelectedIndex = index;
        }

        long SelectedEquipmentId()
        {
            var entry = itemList == null ? null : itemList.SelectedItem as EqEntry;
            return entry == null ? 0 : entry.Row.Id;
        }

        void LoadItems(long equipmentId)
        {
            itemGrid.Rows.Clear();
            if (equipmentId == 0)
            {
                itemTitle.Text = "请选择左侧设备";
                return;
            }
            var equipment = Store.GetEquipment(equipmentId);
            var items = Store.GetItems(equipmentId);
            itemTitle.Text = (equipment == null ? "" : equipment.Code + "  " + equipment.Name) + "  ·  " + items.Count + " 个点检项";

            foreach (var item in items)
            {
                int index = itemGrid.Rows.Add(item.Name, item.Part, item.Unit,
                    item.Lower.ToString("0.###", CultureInfo.InvariantCulture),
                    item.Upper.ToString("0.###", CultureInfo.InvariantCulture),
                    item.Method, item.Enabled ? "启用" : "停用");
                itemGrid.Rows[index].Tag = item;
                if (!item.Enabled) itemGrid.Rows[index].DefaultCellStyle.ForeColor = Ui.Muted;
            }
        }

        CheckItemRow SelectedItem()
        {
            if (itemGrid == null || itemGrid.CurrentRow == null) return null;
            return itemGrid.CurrentRow.Tag as CheckItemRow;
        }

        void AddItem()
        {
            long equipmentId = SelectedEquipmentId();
            if (equipmentId == 0)
            {
                Ui.Info(this, "请先在左侧选择一台设备。");
                return;
            }
            using (var dialog = new ItemDialog(equipmentId, null))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadItems(equipmentId);
                    LoadLogs();
                }
            }
        }

        void EditItem()
        {
            var item = SelectedItem();
            if (item == null)
            {
                Ui.Info(this, "请先选择一个点检项目。");
                return;
            }
            using (var dialog = new ItemDialog(item.EquipmentId, item))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadItems(item.EquipmentId);
                    LoadLogs();
                }
            }
        }

        void DeleteItem()
        {
            var item = SelectedItem();
            if (item == null)
            {
                Ui.Info(this, "请先选择一个点检项目。");
                return;
            }
            if (!Ui.Confirm(this, "确定删除点检项目“" + item.Name + "”吗？")) return;
            try
            {
                Store.DeleteItem(item.Id);
                LoadItems(item.EquipmentId);
                LoadLogs();
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        void ToggleItem()
        {
            var item = SelectedItem();
            if (item == null)
            {
                Ui.Info(this, "请先选择一个点检项目。");
                return;
            }
            Store.SetItemEnabled(item.Id, !item.Enabled);
            LoadItems(item.EquipmentId);
            LoadLogs();
        }

        // ================= 点检任务 =================

        TabPage BuildTaskPage()
        {
            var page = Page("点检任务");
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 2;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = Bar();
            planDate = new DateTimePicker();
            planDate.Format = DateTimePickerFormat.Custom;
            planDate.CustomFormat = "yyyy-MM-dd";
            planDate.Width = 120;
            planDate.Margin = new Padding(0, 5, 10, 0);
            planDate.ValueChanged += delegate { LoadPlans(); };

            planStatus = Ui.Combo(new[] { "全部", Store.PlanPending, Store.PlanDone });
            planStatus.SelectedIndexChanged += delegate { LoadPlans(); };

            var generate = Ui.Primary("生成计划");
            generate.Click += delegate
            {
                int created = Store.GeneratePlans(planDate.Value.Date);
                LoadPlans();
                LoadLogs();
                RefreshStats();
                Ui.Info(this, created > 0
                    ? "已为 " + planDate.Value.ToString("yyyy-MM-dd") + " 生成 " + created + " 张点检计划。"
                    : "该日期计划已存在，无需重复生成。");
            };
            var refresh = Ui.Plain("刷新");
            refresh.Click += delegate { LoadPlans(); };

            operatorBox = Ui.Combo(new string[0]);
            operatorBox.Width = 150;

            bar.Controls.Add(Ui.Lbl("日期"));
            bar.Controls.Add(planDate);
            bar.Controls.Add(generate);
            bar.Controls.Add(Ui.Lbl("状态"));
            bar.Controls.Add(planStatus);
            bar.Controls.Add(refresh);
            bar.Controls.Add(Ui.Hint("操作员"));
            bar.Controls.Add(operatorBox);

            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            taskSplit = split;
            split.SplitterDistance = 250;
            split.SplitterWidth = 8;

            planGrid = Grid();
            AddCol(planGrid, "设备编号", 80);
            AddCol(planGrid, "设备名称", 170);
            AddCol(planGrid, "状态", 70);
            AddCol(planGrid, "项目数", 60);
            AddCol(planGrid, "已录", 55);
            AddCol(planGrid, "异常", 55);
            planGrid.SelectionChanged += delegate { if (!fillingPlans) LoadRecordsForSelection(); };
            split.Panel1.Controls.Add(planGrid);

            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.ColumnCount = 1;
            bottom.RowCount = 2;
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var recordBar = Bar();
            recordTitle = new Label();
            recordTitle.Text = "请选择上方的点检计划";
            recordTitle.AutoSize = true;
            recordTitle.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            recordTitle.ForeColor = Ui.Ink;
            recordTitle.Margin = new Padding(0, 7, 16, 0);

            var save = Ui.Primary("保存当前行判定");
            save.Click += delegate { SaveCurrentRecord(); };
            var hint = Ui.Hint("双击“读数”单元格录入，回车或点击保存；超出限值自动生成工单。");

            recordBar.Controls.Add(recordTitle);
            recordBar.Controls.Add(save);
            recordBar.Controls.Add(hint);

            recordGrid = Grid();
            recordGrid.ReadOnly = false;
            AddCol(recordGrid, "点检项目", 140);
            AddCol(recordGrid, "部位", 100);
            AddCol(recordGrid, "单位", 55);
            AddCol(recordGrid, "下限", 60);
            AddCol(recordGrid, "上限", 60);
            AddCol(recordGrid, "方法", 55);
            AddCol(recordGrid, "读数", 80);
            AddCol(recordGrid, "判定", 55);
            AddCol(recordGrid, "备注", 170);
            AddCol(recordGrid, "录入时间", 120);
            recordGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && !recordGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly)
                    recordGrid.BeginEdit(true);
            };

            bottom.Controls.Add(recordBar, 0, 0);
            bottom.Controls.Add(recordGrid, 0, 1);
            split.Panel2.Controls.Add(bottom);

            table.Controls.Add(bar, 0, 0);
            table.Controls.Add(split, 0, 1);
            page.Controls.Add(table);
            return page;
        }

        void FillOperators()
        {
            var operators = Store.GetOperators();
            operatorBox.Items.Clear();
            foreach (var op in operators) operatorBox.Items.Add(op);
            if (operatorBox.Items.Count > 0) operatorBox.SelectedIndex = 0;
        }

        PlanRow SelectedPlan()
        {
            if (planGrid.CurrentRow == null) return null;
            return planGrid.CurrentRow.Tag as PlanRow;
        }

        void LoadPlans()
        {
            long keep = SelectedPlan() == null ? 0 : SelectedPlan().Id;
            fillingPlans = true;
            planGrid.Rows.Clear();
            string status = planStatus == null ? "全部" : Convert.ToString(planStatus.SelectedItem);
            var plans = Store.GetPlans(planDate.Value.Date, status);
            foreach (var p in plans)
            {
                int index = planGrid.Rows.Add(p.Code, p.Name, p.Status, p.ItemCount, p.DoneCount, p.AbnormalCount);
                planGrid.Rows[index].Tag = p;
                var row = planGrid.Rows[index];
                if (p.Status == Store.PlanDone) row.Cells[2].Style.ForeColor = Color.FromArgb(22, 120, 78);
                else row.Cells[2].Style.ForeColor = Color.FromArgb(180, 95, 6);
                if (p.AbnormalCount > 0) row.Cells[5].Style.ForeColor = Color.FromArgb(180, 60, 40);
            }
            fillingPlans = false;

            if (planGrid.Rows.Count > 0)
            {
                int index = 0;
                for (int i = 0; i < planGrid.Rows.Count; i++)
                {
                    var plan = planGrid.Rows[i].Tag as PlanRow;
                    if (plan != null && plan.Id == keep) index = i;
                }
                planGrid.CurrentCell = planGrid.Rows[index].Cells[0];
            }
            LoadRecordsForSelection();
        }

        void LoadRecordsForSelection()
        {
            recordGrid.Rows.Clear();
            var plan = SelectedPlan();
            if (plan == null)
            {
                recordTitle.Text = "请选择上方的点检计划";
                return;
            }

            bool locked = plan.Status == Store.PlanDone;
            recordTitle.Text = plan.Code + " " + plan.Name + "  ·  " + plan.PlanDate + "  ·  " + plan.Status +
                "  ·  已录 " + plan.DoneCount + "/" + plan.ItemCount;

            foreach (var record in Store.GetPlanRecords(plan.Id))
            {
                int index = recordGrid.Rows.Add(record.Name, record.Part, record.Unit,
                    record.Lower.ToString("0.###", CultureInfo.InvariantCulture),
                    record.Upper.ToString("0.###", CultureInfo.InvariantCulture),
                    record.Method,
                    record.HasValue ? record.Value.ToString("0.###", CultureInfo.InvariantCulture) : "",
                    record.Judged, record.Note, record.CheckedAt);
                var row = recordGrid.Rows[index];
                row.Tag = record;
                for (int c = 0; c < row.Cells.Count; c++)
                {
                    bool editable = (c == ValueColumn || c == NoteColumn) && !locked;
                    row.Cells[c].ReadOnly = !editable;
                }
                if (record.Judged == Store.JudgeBad)
                {
                    row.Cells[JudgeColumn].Style.ForeColor = Color.FromArgb(180, 60, 40);
                    row.Cells[JudgeColumn].Style.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                }
                else if (record.Judged == Store.JudgeOk)
                {
                    row.Cells[JudgeColumn].Style.ForeColor = Color.FromArgb(22, 120, 78);
                }
            }
        }

        void SaveCurrentRecord()
        {
            var plan = SelectedPlan();
            if (plan == null)
            {
                Ui.Info(this, "请先选择一张点检计划。");
                return;
            }
            if (plan.Status == Store.PlanDone)
            {
                Ui.Info(this, "该计划已完成，读数只读。");
                return;
            }
            var row = recordGrid.CurrentRow;
            if (row == null)
            {
                Ui.Info(this, "请先选择一条点检项目。");
                return;
            }
            recordGrid.EndEdit();

            string text = Convert.ToString(row.Cells[ValueColumn].Value).Trim();
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                Ui.Error(this, "读数必须填写数字，例如 2.1。");
                return;
            }
            var record = row.Tag as RecordRow;
            var op = operatorBox.SelectedItem as OperatorRow;
            if (record == null || op == null)
            {
                Ui.Error(this, "缺少点检项目或操作员信息。");
                return;
            }
            string note = Convert.ToString(row.Cells[NoteColumn].Value).Trim();

            try
            {
                Store.SaveRecord(plan.Id, record.ItemId, value, note, op.Id);
                LoadPlans();
                LoadOrders();
                LoadEquipments();
                LoadLogs();
                RefreshStats();
                statusRight.Text = "已保存：" + record.Name + " = " + value.ToString("0.###", CultureInfo.InvariantCulture) + record.Unit;
            }
            catch (DbException ex)
            {
                Ui.Error(this, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        // ================= 维修工单 =================

        TabPage BuildOrderPage()
        {
            var page = Page("维修工单");
            var table = Stack();
            var bar = Bar();

            orderStatus = Ui.Combo(new[] { "全部", Store.OrderPending, Store.OrderWorking, Store.OrderFinished, Store.OrderClosed });
            orderStatus.SelectedIndexChanged += delegate { LoadOrders(); };

            var refresh = Ui.Plain("刷新");
            refresh.Click += delegate { LoadOrders(); };
            var start = Ui.Primary("开始处理");
            start.Click += delegate { StartOrder(); };
            var complete = Ui.Plain("完成");
            complete.Click += delegate { CompleteOrder(); };
            var close = Ui.Plain("关闭");
            close.Click += delegate { CloseOrder(); };
            var detail = Ui.Plain("详情");
            detail.Click += delegate { ShowOrderDetail(); };

            bar.Controls.Add(Ui.Lbl("状态"));
            bar.Controls.Add(orderStatus);
            bar.Controls.Add(refresh);
            bar.Controls.Add(start);
            bar.Controls.Add(complete);
            bar.Controls.Add(close);
            bar.Controls.Add(detail);

            orderGrid = Grid();
            AddCol(orderGrid, "工单号", 130);
            AddCol(orderGrid, "设备编号", 80);
            AddCol(orderGrid, "设备名称", 130);
            AddCol(orderGrid, "级别", 55);
            AddCol(orderGrid, "状态", 65);
            AddCol(orderGrid, "异常描述", 320);
            AddCol(orderGrid, "责任人", 65);
            AddCol(orderGrid, "创建时间", 125);

            table.Controls.Add(bar, 0, 0);
            table.Controls.Add(orderGrid, 0, 1);
            page.Controls.Add(table);
            return page;
        }

        void LoadOrders()
        {
            string status = orderStatus == null ? "全部" : Convert.ToString(orderStatus.SelectedItem);
            orderGrid.Rows.Clear();
            foreach (var o in Store.GetOrders(status))
            {
                int index = orderGrid.Rows.Add(o.OrderNo, o.Code, o.Name, o.Level, o.Status,
                    o.Description, o.Assignee, o.CreatedAt);
                var row = orderGrid.Rows[index];
                row.Tag = o;
                if (o.Level == "紧急") row.Cells[3].Style.ForeColor = Color.FromArgb(180, 60, 40);
                if (o.Status == Store.OrderClosed) row.DefaultCellStyle.ForeColor = Ui.Muted;
                else if (o.Status == Store.OrderFinished) row.Cells[4].Style.ForeColor = Color.FromArgb(22, 120, 78);
                else if (o.Status == Store.OrderWorking) row.Cells[4].Style.ForeColor = Color.FromArgb(31, 95, 168);
            }
        }

        OrderRow SelectedOrder()
        {
            if (orderGrid.CurrentRow == null) return null;
            return orderGrid.CurrentRow.Tag as OrderRow;
        }

        void StartOrder()
        {
            var order = SelectedOrder();
            if (order == null)
            {
                Ui.Info(this, "请先选择一张工单。");
                return;
            }
            if (order.Status != Store.OrderPending)
            {
                Ui.Info(this, "只有“待处理”的工单可以开始处理。");
                return;
            }
            string assignee = Prompt.Ask(this, "开始处理", "处理人（维修工）", "王强", false, true);
            if (assignee == null) return;
            try
            {
                Store.StartOrder(order.Id, assignee);
                LoadOrders();
                LoadLogs();
                RefreshStats();
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        void CompleteOrder()
        {
            var order = SelectedOrder();
            if (order == null)
            {
                Ui.Info(this, "请先选择一张工单。");
                return;
            }
            if (order.Status != Store.OrderWorking)
            {
                Ui.Info(this, "只有“处理中”的工单可以完成。");
                return;
            }
            string solution = Prompt.Ask(this, "完成工单", "处理措施（必填）", "", true, true);
            if (solution == null) return;
            try
            {
                Store.CompleteOrder(order.Id, solution);
                LoadOrders();
                LoadLogs();
                RefreshStats();
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        void CloseOrder()
        {
            var order = SelectedOrder();
            if (order == null)
            {
                Ui.Info(this, "请先选择一张工单。");
                return;
            }
            if (order.Status != Store.OrderFinished)
            {
                Ui.Info(this, "只有“已完成”的工单可以关闭。");
                return;
            }
            if (!Ui.Confirm(this, "确认关闭工单 " + order.OrderNo + " 吗？关闭后不再出现在未关闭工单统计中。")) return;
            try
            {
                Store.CloseOrder(order.Id);
                LoadOrders();
                LoadEquipments();
                LoadLogs();
                RefreshStats();
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        void ShowOrderDetail()
        {
            var order = SelectedOrder();
            if (order == null)
            {
                Ui.Info(this, "请先选择一张工单。");
                return;
            }
            string text =
                "工单号：" + order.OrderNo + Environment.NewLine +
                "设备：" + order.Code + " " + order.Name + Environment.NewLine +
                "级别：" + order.Level + "    状态：" + order.Status + Environment.NewLine +
                "责任人：" + (order.Assignee.Length == 0 ? "未指派" : order.Assignee) + Environment.NewLine +
                "创建时间：" + order.CreatedAt + Environment.NewLine +
                "开始时间：" + (order.StartedAt.Length == 0 ? "—" : order.StartedAt) + Environment.NewLine +
                "关闭时间：" + (order.ClosedAt.Length == 0 ? "—" : order.ClosedAt) + Environment.NewLine +
                Environment.NewLine + "异常描述：" + Environment.NewLine + order.Description + Environment.NewLine +
                Environment.NewLine + "处理措施：" + Environment.NewLine +
                (order.Solution.Length == 0 ? "（尚未填写）" : order.Solution);
            MessageBox.Show(this, text, "工单详情 " + order.OrderNo, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ================= 统计报表 =================

        TabPage BuildStatPage()
        {
            var page = Page("统计报表");
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 3;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = Bar();
            statFrom = new DateTimePicker();
            statFrom.Format = DateTimePickerFormat.Custom;
            statFrom.CustomFormat = "yyyy-MM-dd";
            statFrom.Width = 120;
            statFrom.Margin = new Padding(0, 5, 10, 0);
            statFrom.Value = DateTime.Today.AddDays(-6);

            statTo = new DateTimePicker();
            statTo.Format = DateTimePickerFormat.Custom;
            statTo.CustomFormat = "yyyy-MM-dd";
            statTo.Width = 120;
            statTo.Margin = new Padding(0, 5, 10, 0);
            statTo.Value = DateTime.Today;

            var compute = Ui.Primary("统计");
            compute.Click += delegate { RefreshStats(); };
            var export = Ui.Plain("导出 CSV");
            export.Click += delegate { ExportStats(); };

            bar.Controls.Add(Ui.Lbl("起始日期"));
            bar.Controls.Add(statFrom);
            bar.Controls.Add(Ui.Lbl("至"));
            bar.Controls.Add(statTo);
            bar.Controls.Add(compute);
            bar.Controls.Add(export);

            var cards = new FlowLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.WrapContents = false;
            cards.Padding = new Padding(0, 4, 0, 0);
            kpiValues = new Label[6];
            cards.Controls.Add(KpiCard("在用在册设备", 0));
            cards.Controls.Add(KpiCard("点检计划", 1));
            cards.Controls.Add(KpiCard("计划完成率", 2));
            cards.Controls.Add(KpiCard("异常率", 3));
            cards.Controls.Add(KpiCard("设备完好率", 4));
            cards.Controls.Add(KpiCard("未关闭工单", 5));

            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            statSplit = split;
            split.SplitterDistance = 230;
            split.SplitterWidth = 8;

            statGrid = Grid();
            AddCol(statGrid, "设备编号", 80);
            AddCol(statGrid, "设备名称", 140);
            AddCol(statGrid, "计划数", 65);
            AddCol(statGrid, "完成数", 65);
            AddCol(statGrid, "完成率", 75);
            AddCol(statGrid, "记录数", 65);
            AddCol(statGrid, "异常数", 65);
            AddCol(statGrid, "异常率", 75);
            AddCol(statGrid, "未关闭工单", 85);
            AddCol(statGrid, "最近点检", 100);

            abnormalGrid = Grid();
            AddCol(abnormalGrid, "排名", 55);
            AddCol(abnormalGrid, "点检项目", 190);
            AddCol(abnormalGrid, "设备编号", 90);
            AddCol(abnormalGrid, "设备名称", 170);
            AddCol(abnormalGrid, "异常次数", 90);

            split.Panel1.Controls.Add(Section("按设备汇总", statGrid));
            split.Panel2.Controls.Add(Section("异常项排行（Top 10）", abnormalGrid));

            table.Controls.Add(bar, 0, 0);
            table.Controls.Add(cards, 0, 1);
            table.Controls.Add(split, 0, 2);
            page.Controls.Add(table);
            return page;
        }

        static Control Section(string caption, Control content)
        {
            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 1;
            table.RowCount = 2;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            label.ForeColor = Ui.Muted;
            label.Margin = new Padding(0, 2, 0, 4);

            table.Controls.Add(label, 0, 0);
            table.Controls.Add(content, 0, 1);
            return table;
        }

        Panel KpiCard(string caption, int index)
        {
            var card = new Panel();
            card.Width = 172;
            card.Height = 76;
            card.BackColor = Color.White;
            card.Margin = new Padding(0, 0, 12, 0);
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Ui.Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var value = new Label();
            value.Text = "—";
            value.Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
            value.ForeColor = Ui.Accent;
            value.AutoSize = true;
            value.Location = new Point(14, 8);

            var label = new Label();
            label.Text = caption;
            label.ForeColor = Ui.Muted;
            label.AutoSize = true;
            label.Location = new Point(15, 46);

            card.Controls.Add(value);
            card.Controls.Add(label);
            kpiValues[index] = value;
            return card;
        }

        void RefreshStats()
        {
            if (statFrom == null || statTo == null) return;
            try
            {
                var stats = Store.ComputeStats(statFrom.Value.Date, statTo.Value.Date);
                kpiValues[0].Text = stats.Equipments.ToString(CultureInfo.InvariantCulture);
                kpiValues[1].Text = stats.Plans.ToString(CultureInfo.InvariantCulture);
                kpiValues[2].Text = stats.CompletionRate.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                kpiValues[3].Text = stats.AbnormalRate.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                kpiValues[4].Text = stats.HealthRate.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                kpiValues[5].Text = stats.OpenOrders.ToString(CultureInfo.InvariantCulture);
                kpiValues[3].ForeColor = stats.Abnormal > 0 ? Color.FromArgb(180, 60, 40) : Color.FromArgb(22, 120, 78);
                kpiValues[4].ForeColor = stats.HealthRate >= 100 ? Color.FromArgb(22, 120, 78) : Ui.Accent;

                statGrid.Rows.Clear();
                foreach (var e in stats.ByEquipment)
                {
                    double completion = e.Plans == 0 ? 0 : e.Done * 100.0 / e.Plans;
                    double abnormal = e.Records == 0 ? 0 : e.Abnormal * 100.0 / e.Records;
                    int index = statGrid.Rows.Add(e.Code, e.Name, e.Plans, e.Done,
                        completion.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                        e.Records, e.Abnormal,
                        abnormal.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                        e.OpenOrders, e.LastCheck);
                    if (e.Abnormal > 0) statGrid.Rows[index].Cells[6].Style.ForeColor = Color.FromArgb(180, 60, 40);
                    if (e.OpenOrders > 0) statGrid.Rows[index].Cells[8].Style.ForeColor = Color.FromArgb(180, 60, 40);
                }

                abnormalGrid.Rows.Clear();
                for (int i = 0; i < stats.TopAbnormal.Count; i++)
                {
                    var a = stats.TopAbnormal[i];
                    abnormalGrid.Rows.Add(i + 1, a.ItemName, a.Code, a.EquipmentName, a.Count);
                }

                statusRight.Text = "统计区间 " + stats.From.ToString("yyyy-MM-dd") + " 至 " + stats.To.ToString("yyyy-MM-dd");
            }
            catch (InvalidOperationException ex)
            {
                Ui.Error(this, ex.Message);
            }
        }

        void ExportStats()
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "CSV 文件|*.csv";
            dialog.FileName = "点检统计_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                Store.ExportStats(dialog.FileName, Store.ComputeStats(statFrom.Value.Date, statTo.Value.Date));
                Ui.Info(this, "统计报表已导出：" + dialog.FileName);
            }
            catch (Exception ex)
            {
                Ui.Error(this, "导出失败：" + ex.Message);
            }
        }

        // ================= 操作日志 =================

        TabPage BuildLogPage()
        {
            var page = Page("操作日志");
            var table = Stack();
            var bar = Bar();
            var hint = Ui.Hint("记录最近的录入、开单、状态变更与数据维护操作，最多显示 200 条。");
            var refresh = Ui.Primary("刷新");
            refresh.Click += delegate { LoadLogs(); };
            bar.Controls.Add(hint);
            bar.Controls.Add(refresh);

            logGrid = Grid();
            AddCol(logGrid, "时间", 140);
            AddCol(logGrid, "动作", 80);
            AddCol(logGrid, "对象", 80);
            AddCol(logGrid, "详情", 600);

            table.Controls.Add(bar, 0, 0);
            table.Controls.Add(logGrid, 0, 1);
            page.Controls.Add(table);
            return page;
        }

        void LoadLogs()
        {
            logGrid.Rows.Clear();
            foreach (var log in Store.GetAudit(200))
                logGrid.Rows.Add(log.CreatedAt, log.Action, log.Entity, log.Detail);
        }

        // ================= 菜单动作 =================

        void BackupDatabase()
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "SQLite 数据库|*.db";
            dialog.FileName = "inspection-backup-" + DateTime.Today.ToString("yyyyMMdd") + ".db";
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                Store.Backup(dialog.FileName);
                Ui.Info(this, "数据库已备份到：" + dialog.FileName);
            }
            catch (Exception ex)
            {
                Ui.Error(this, "备份失败：" + ex.Message);
            }
        }

        void OpenDataFolder()
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + Store.DataDir + "\"");
                System.Diagnostics.Process.Start(info);
            }
            catch (Exception ex)
            {
                Ui.Error(this, "无法打开目录：" + ex.Message);
            }
        }

        void ResetDatabase()
        {
            if (!Ui.Confirm(this, "将删除当前数据库并重新写入示例数据，此操作不可撤销。确定继续吗？")) return;
            try
            {
                Store.ResetDatabase();
                FillOperators();
                LoadEquipments();
                LoadEquipmentList();
                LoadPlans();
                LoadOrders();
                LoadLogs();
                RefreshStats();
                statusDb.Text = "数据库：" + Store.DatabasePath;
                Ui.Info(this, "示例数据已重新生成。");
            }
            catch (Exception ex)
            {
                Ui.Error(this, "重置失败：" + ex.Message);
            }
        }

        void ShowAbout()
        {
            string text = "设备点检与异常工单管理系统  v" + Program.AppVersion + Environment.NewLine +
                Environment.NewLine +
                "面向设备日常点检场景的单机桌面应用：维护设备台账与点检标准，按日生成点检计划，" +
                "录入读数后按上下限自动判定，异常自动生成维修工单并跟踪到关闭，最后汇总为统计报表。" + Environment.NewLine +
                Environment.NewLine +
                "技术栈：C# WinForms + SQLite（Windows 自带 winsqlite3.dll，无需安装数据库服务）。" + Environment.NewLine +
                "数据库：" + Store.DatabasePath;
            MessageBox.Show(this, text, "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ================= 表格工具 =================

        sealed class GridView : DataGridView
        {
            public GridView() { DoubleBuffered = true; }
        }

        static DataGridView Grid()
        {
            var grid = new GridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToOrderColumns = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Ui.SoftBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ui.Ink;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Ui.SoftBack;
            grid.GridColor = Color.FromArgb(230, 235, 241);
            grid.RowTemplate.Height = 30;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Ui.Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 40, 70);
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            return grid;
        }

        static void AddCol(DataGridView grid, string header, int weight)
        {
            var column = new DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.FillWeight = weight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.Resizable = DataGridViewTriState.False;
            grid.Columns.Add(column);
        }
    }
}