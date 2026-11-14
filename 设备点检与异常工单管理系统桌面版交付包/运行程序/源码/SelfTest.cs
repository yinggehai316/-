using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Inspection
{
    /// <summary>无界面自检：建库、种子、规则、状态机、统计、导出逐项断言。</summary>
    static class SelfTest
    {
        static int passed;
        static int failed;
        static readonly List<string> lines = new List<string>();

        public static int Run()
        {
            passed = 0;
            failed = 0;
            lines.Clear();
            string dir = Path.Combine(Path.GetTempPath(), "inspection-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Store.Open(dir);
                TestSchemaAndSeed();
                TestEquipmentQueries();
                TestJudgeRules();
                TestStats();
                TestRecordAndOrderFlow();
                TestPlanCompletion();
                TestOrderStateMachine();
                TestPlanGeneration();
                TestExportAndBackup();
                TestDeleteProtection();
            }
            catch (Exception ex)
            {
                failed++;
                lines.Add("[FAIL] 自检异常中断 — " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                Store.Close();
                try { Directory.Delete(dir, true); } catch { }
            }

            lines.Add("");
            lines.Add(string.Format(CultureInfo.InvariantCulture, "结果：通过 {0} 项，失败 {1} 项。", passed, failed));
            string text = string.Join(Environment.NewLine, lines.ToArray());
            string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "自检报告.txt");
            try { File.WriteAllText(report, text, new UTF8Encoding(true)); } catch { }
            try { Console.WriteLine(text); } catch { }
            return failed == 0 ? 0 : 1;
        }

        static void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                passed++;
                lines.Add("[PASS] " + name);
            }
            else
            {
                failed++;
                lines.Add("[FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : " — " + detail));
            }
        }

        static void TestSchemaAndSeed()
        {
            long equipment = Store.GetEquipments("", "全部").Count;
            Check("种子：设备 6 台", equipment == 6, "实际 " + equipment);

            long items = 0;
            foreach (var e in Store.GetEquipments("", "全部")) items += Store.GetItems(e.Id).Count;
            Check("种子：点检项 18 个", items == 18, "实际 " + items);

            long plans = Store.GetPlans(DateTime.Today, "全部").Count;
            Check("种子：今日计划 5 张", plans == 5, "实际 " + plans);
            long pending = Store.GetPlans(DateTime.Today, Store.PlanPending).Count;
            Check("种子：今日待点检 2 张", pending == 2, "实际 " + pending);

            long orders = Store.GetOrders("全部").Count;
            Check("种子：工单 3 张", orders == 3, "实际 " + orders);
            long open = Store.GetOrders(Store.OrderPending).Count + Store.GetOrders(Store.OrderWorking).Count;
            Check("种子：未关闭工单 2 张", open == 2, "实际 " + open);

            Check("数据库文件已生成", File.Exists(Store.DatabasePath) && new FileInfo(Store.DatabasePath).Length > 0, Store.DatabasePath);
        }

        static void TestEquipmentQueries()
        {
            long inUse = Store.GetEquipments("", Store.StatusInUse).Count;
            Check("筛选：在用在册 5 台", inUse == 5, "实际 " + inUse);

            long search = Store.GetEquipments("磨床", "全部").Count;
            Check("搜索：按名称命中外圆磨床", search == 1, "实际 " + search);

            var eq = Store.GetEquipments("EQ-006", "全部");
            Check("搜索：EQ-006 为停用", eq.Count == 1 && eq[0].Status == Store.StatusStopped, eq.Count == 1 ? eq[0].Status : "未找到");

            var eq3 = Store.GetEquipments("EQ-003", "全部");
            Check("视图：EQ-003 未关闭工单 1 张", eq3.Count == 1 && eq3[0].OpenOrders == 1, eq3.Count == 1 ? eq3[0].OpenOrders.ToString(CultureInfo.InvariantCulture) : "未找到");
        }

        static void TestJudgeRules()
        {
            var item = new CheckItemRow();
            item.Lower = 1400;
            item.Upper = 1600;
            item.Name = "砂轮转速";
            item.Unit = "r/min";

            Check("判定：等于上限为正常", Store.Judge(item, 1600) == Store.JudgeOk, "");
            Check("判定：略超上限为异常", Store.Judge(item, 1600.1) == Store.JudgeBad, "");
            Check("判定：低于下限为异常", Store.Judge(item, 1399.9) == Store.JudgeBad, "");
            Check("级别：超限 2.5% 为一般", !Store.IsUrgent(item, 1640), "");
            Check("级别：超限 3.4% 为紧急", Store.IsUrgent(item, 1655), "");
            Check("级别：低于下限 10% 为紧急", Store.IsUrgent(item, 1350), "");
        }

        static void TestStats()
        {
            DateTime from = DateTime.Today.AddDays(-1);
            var stats = Store.ComputeStats(from, DateTime.Today);
            Check("统计：计划 10 张", stats.Plans == 10, "实际 " + stats.Plans);
            Check("统计：完成 8 张", stats.DonePlans == 8, "实际 " + stats.DonePlans);
            Check("统计：完成率 80%", Math.Abs(stats.CompletionRate - 80) < 0.001, stats.CompletionRate.ToString("0.00", CultureInfo.InvariantCulture));
            Check("统计：记录 26 条", stats.Records == 26, "实际 " + stats.Records);
            Check("统计：异常 2 条", stats.Abnormal == 2, "实际 " + stats.Abnormal);
            Check("统计：异常率 7.69%", Math.Abs(stats.AbnormalRate - 7.6923) < 0.01, stats.AbnormalRate.ToString("0.00", CultureInfo.InvariantCulture));
            Check("统计：设备完好率 60%", Math.Abs(stats.HealthRate - 60) < 0.001, stats.HealthRate.ToString("0.00", CultureInfo.InvariantCulture));
            Check("统计：按设备汇总 6 行", stats.ByEquipment.Count == 6, "实际 " + stats.ByEquipment.Count);
            Check("统计：异常排行 2 行", stats.TopAbnormal.Count == 2, "实际 " + stats.TopAbnormal.Count);
        }

        static void TestRecordAndOrderFlow()
        {
            var plan = Store.GetPlans(DateTime.Today, "全部").Find(delegate(PlanRow p) { return p.Code == "EQ-003"; });
            Check("流程：找到 EQ-003 今日计划", plan != null, "");
            if (plan == null) return;

            List<RecordRow> records = Store.GetPlanRecords(plan.Id);
            Check("流程：EQ-003 计划含 3 个点检项", records.Count == 3, "实际 " + records.Count);

            RecordRow target = records.Find(delegate(RecordRow r) { return r.Name == "砂轮转速"; });
            if (target == null)
            {
                Check("流程：找到砂轮转速项", false, "");
                return;
            }

            long operatorId = Store.GetOperators()[0].Id;
            int seededOrders = CountOrdersFor("砂轮转速");
            Store.SaveRecord(plan.Id, target.ItemId, 1700, "自检录入", operatorId);
            Check("流程：已有未关闭工单时不重复开单", CountOrdersFor("砂轮转速") == seededOrders, "实际 " + CountOrdersFor("砂轮转速"));

            var saved = Store.GetPlanRecords(plan.Id).Find(delegate(RecordRow r) { return r.ItemId == target.ItemId; });
            Check("流程：读数已保存且判定异常", saved != null && saved.HasValue && saved.Judged == Store.JudgeBad, "");

            Store.SaveRecord(plan.Id, target.ItemId, 1705, "自检复录", operatorId);
            Check("流程：重复录入仍不重复开单", CountOrdersFor("砂轮转速") == seededOrders, "实际 " + CountOrdersFor("砂轮转速"));

            var plan5 = Store.GetPlans(DateTime.Today, "全部").Find(delegate(PlanRow p) { return p.Code == "EQ-005"; });
            if (plan5 == null)
            {
                Check("流程：找到 EQ-005 今日计划", false, "");
                return;
            }
            RecordRow pressure = Store.GetPlanRecords(plan5.Id).Find(delegate(RecordRow r) { return r.Name == "排气压力"; });
            if (pressure == null)
            {
                Check("流程：找到排气压力项", false, "");
                return;
            }

            int before = CountOrdersFor("排气压力");
            Store.SaveRecord(plan5.Id, pressure.ItemId, 1.2, "自检录入", operatorId);
            int after = CountOrdersFor("排气压力");
            Check("流程：异常读数自动开单", after == before + 1, "前 " + before + " 后 " + after);

            var order = Store.GetOrders(Store.OrderPending).Find(delegate(OrderRow o) { return o.Code == "EQ-005"; });
            Check("流程：新工单级别为紧急", order != null && order.Level == "紧急", order == null ? "未找到" : order.Level);
        }

        static void TestPlanCompletion()
        {
            var plan = Store.GetPlans(DateTime.Today, "全部").Find(delegate(PlanRow p) { return p.Code == "EQ-005"; });
            if (plan == null) return;
            long operatorId = Store.GetOperators()[0].Id;
            foreach (var record in Store.GetPlanRecords(plan.Id))
            {
                if (record.HasValue) continue;
                Store.SaveRecord(plan.Id, record.ItemId, (record.Lower + record.Upper) / 2.0, "自检补录", operatorId);
            }
            var refreshed = Store.GetPlan(plan.Id);
            Check("流程：项目录满后计划自动完成", refreshed != null && refreshed.Status == Store.PlanDone, refreshed == null ? "未找到" : refreshed.Status);
        }

        static void TestOrderStateMachine()
        {
            var order = Store.GetOrders(Store.OrderPending).Find(delegate(OrderRow o) { return o.Code == "EQ-005"; });
            if (order == null)
            {
                Check("工单：找到待处理工单", false, "");
                return;
            }

            bool blocked = false;
            try { Store.CompleteOrder(order.Id, "未开始就完成"); } catch (InvalidOperationException) { blocked = true; }
            Check("工单：待处理不能直接完成", blocked, "");

            Store.StartOrder(order.Id, "王强");
            Check("工单：开始处理 → 处理中", Store.GetOrder(order.Id).Status == Store.OrderWorking, Store.GetOrder(order.Id).Status);

            blocked = false;
            try { Store.CompleteOrder(order.Id, ""); } catch (InvalidOperationException) { blocked = true; }
            Check("工单：必须填写处理措施", blocked, "");

            Store.CompleteOrder(order.Id, "调整砂轮动平衡，复测 1520 r/min");
            Check("工单：完成 → 已完成", Store.GetOrder(order.Id).Status == Store.OrderFinished, Store.GetOrder(order.Id).Status);

            Store.CloseOrder(order.Id);
            Check("工单：关闭 → 已关闭", Store.GetOrder(order.Id).Status == Store.OrderClosed, Store.GetOrder(order.Id).Status);

            blocked = false;
            try { Store.StartOrder(order.Id, "王强"); } catch (InvalidOperationException) { blocked = true; }
            Check("工单：已关闭不能重新开始", blocked, "");
        }

        static void TestPlanGeneration()
        {
            DateTime tomorrow = DateTime.Today.AddDays(1);
            int created = Store.GeneratePlans(tomorrow);
            Check("计划：为明天生成 5 张", created == 5, "实际 " + created);
            int again = Store.GeneratePlans(tomorrow);
            Check("计划：重复生成返回 0", again == 0, "实际 " + again);
        }

        static void TestExportAndBackup()
        {
            string csv = Path.Combine(Path.GetTempPath(), "inspection-export-" + Guid.NewGuid().ToString("N") + ".csv");
            string bak = Path.Combine(Path.GetTempPath(), "inspection-backup-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                Store.ExportStats(csv, Store.ComputeStats(DateTime.Today.AddDays(-1), DateTime.Today));
                string text = File.ReadAllText(csv, Encoding.UTF8);
                Check("导出：CSV 含统计区间", text.Contains("统计区间"), "");
                Check("导出：CSV 含按设备汇总", text.Contains("设备编号,设备名称,计划数"), "");
                Check("导出：CSV 含 EQ-006 行", text.Contains("EQ-006"), "");

                Store.Backup(bak);
                Check("备份：副本已生成且非空", File.Exists(bak) && new FileInfo(bak).Length > 0, "");
            }
            finally
            {
                try { File.Delete(csv); } catch { }
                try { File.Delete(bak); } catch { }
            }
        }

        static void TestDeleteProtection()
        {
            var eq = Store.GetEquipments("EQ-001", "全部");
            if (eq.Count == 0) return;
            bool deleted = Store.DeleteEquipment(eq[0].Id);
            Check("删除：有历史记录的设备被拦截", !deleted, "返回 true");
        }

        static int CountOrdersFor(string keyword)
        {
            int count = 0;
            foreach (var order in Store.GetOrders("全部"))
                if (order.Description.IndexOf(keyword, StringComparison.Ordinal) >= 0) count++;
            return count;
        }
    }
}