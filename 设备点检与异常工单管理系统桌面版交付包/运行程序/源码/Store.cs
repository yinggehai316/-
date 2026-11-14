using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Inspection
{
    sealed class EquipmentRow
    {
        public long Id;
        public string Code = "";
        public string Name = "";
        public string Model = "";
        public string Location = "";
        public string Status = "";
        public string CommissionedAt = "";
        public string Remark = "";
        public int ItemCount;
        public int OpenOrders;
        public string LastCheck = "";
    }

    sealed class CheckItemRow
    {
        public long Id;
        public long EquipmentId;
        public string Name = "";
        public string Part = "";
        public string Unit = "";
        public double Lower;
        public double Upper;
        public string Method = "";
        public bool Enabled;
    }

    sealed class OperatorRow
    {
        public long Id;
        public string Name = "";
        public string Role = "";

        public override string ToString()
        {
            return Role.Length == 0 ? Name : Name + "（" + Role + "）";
        }
    }

    sealed class PlanRow
    {
        public long Id;
        public long EquipmentId;
        public string PlanDate = "";
        public string Code = "";
        public string Name = "";
        public string Status = "";
        public int ItemCount;
        public int DoneCount;
        public int AbnormalCount;
    }

    sealed class RecordRow
    {
        public long ItemId;
        public string Name = "";
        public string Part = "";
        public string Unit = "";
        public double Lower;
        public double Upper;
        public string Method = "";
        public bool HasValue;
        public double Value;
        public string Judged = "";
        public string Note = "";
        public string CheckedAt = "";
    }

    sealed class OrderRow
    {
        public long Id;
        public string OrderNo = "";
        public long EquipmentId;
        public string Code = "";
        public string Name = "";
        public string Level = "";
        public string Status = "";
        public string Description = "";
        public string Assignee = "";
        public string Solution = "";
        public string CreatedAt = "";
        public string StartedAt = "";
        public string ClosedAt = "";
    }

    sealed class AuditRow
    {
        public long Id;
        public string Action = "";
        public string Entity = "";
        public string Detail = "";
        public string CreatedAt = "";
    }

    sealed class EquipStat
    {
        public string Code = "";
        public string Name = "";
        public int Plans;
        public int Done;
        public int Records;
        public int Abnormal;
        public int OpenOrders;
        public string LastCheck = "";
    }

    sealed class AbnormalStat
    {
        public string ItemName = "";
        public string Code = "";
        public string EquipmentName = "";
        public int Count;
    }

    sealed class Stats
    {
        public DateTime From;
        public DateTime To;
        public int Equipments;
        public int Plans;
        public int DonePlans;
        public int Records;
        public int Abnormal;
        public int OpenOrders;
        public int ClosedOrders;
        public int EquipmentsWithOpen;
        public List<EquipStat> ByEquipment = new List<EquipStat>();
        public List<AbnormalStat> TopAbnormal = new List<AbnormalStat>();

        public double CompletionRate { get { return Plans == 0 ? 0 : DonePlans * 100.0 / Plans; } }
        public double AbnormalRate { get { return Records == 0 ? 0 : Abnormal * 100.0 / Records; } }
        public double HealthRate { get { return Equipments == 0 ? 0 : (Equipments - EquipmentsWithOpen) * 100.0 / Equipments; } }
        public double CloseRate
        {
            get { int total = OpenOrders + ClosedOrders; return total == 0 ? 0 : ClosedOrders * 100.0 / total; }
        }
    }

    /// <summary>业务层：建表、种子数据、增删改查、判定规则、统计与导出。</summary>
    static class Store
    {
        public const string StatusInUse = "在用";
        public const string StatusStopped = "停用";
        public const string StatusRepair = "维修中";

        public const string PlanPending = "待点检";
        public const string PlanDone = "已完成";

        public const string JudgeOk = "正常";
        public const string JudgeBad = "异常";

        public const string OrderPending = "待处理";
        public const string OrderWorking = "处理中";
        public const string OrderFinished = "已完成";
        public const string OrderClosed = "已关闭";

        public static readonly string[] EquipmentStatuses = { StatusInUse, StatusStopped, StatusRepair };
        public static readonly string[] Methods = { "目视", "测温", "测压", "测振", "测速", "测流", "听音" };

        static Db db;

        public static string DataDir { get; private set; }
        public static string DatabasePath { get; private set; }

        public static void Open(string dataDir)
        {
            DataDir = dataDir;
            Directory.CreateDirectory(dataDir);
            DatabasePath = Path.Combine(dataDir, "inspection.db");
            db = new Db(DatabasePath);
            EnsureSchema();
            if (Count("SELECT COUNT(*) AS n FROM equipment") == 0) Seed();
        }

        public static void Close()
        {
            if (db != null)
            {
                db.Dispose();
                db = null;
            }
        }

        public static void ResetDatabase()
        {
            Close();
            if (File.Exists(DatabasePath)) File.Delete(DatabasePath);
            Open(DataDir);
        }

        public static void Backup(string target)
        {
            File.Copy(DatabasePath, target, true);
        }

        // ---------- 建表 ----------

        const string Schema = @"
PRAGMA user_version = 1;

CREATE TABLE IF NOT EXISTS equipment (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    code            TEXT    NOT NULL UNIQUE,
    name            TEXT    NOT NULL,
    model           TEXT    NOT NULL DEFAULT '',
    location        TEXT    NOT NULL DEFAULT '',
    status          TEXT    NOT NULL DEFAULT '在用' CHECK (status IN ('在用','停用','维修中')),
    commissioned_at TEXT    NOT NULL DEFAULT '',
    remark          TEXT    NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS check_item (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    equipment_id INTEGER NOT NULL REFERENCES equipment(id) ON DELETE CASCADE,
    name         TEXT    NOT NULL,
    part         TEXT    NOT NULL DEFAULT '',
    unit         TEXT    NOT NULL DEFAULT '',
    lower        REAL    NOT NULL,
    upper        REAL    NOT NULL,
    method       TEXT    NOT NULL DEFAULT '目视',
    enabled      INTEGER NOT NULL DEFAULT 1 CHECK (enabled IN (0,1)),
    UNIQUE (equipment_id, name),
    CHECK (lower <= upper)
);

CREATE TABLE IF NOT EXISTS operator (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    name    TEXT    NOT NULL UNIQUE,
    role    TEXT    NOT NULL DEFAULT '点检员' CHECK (role IN ('点检员','维修工','管理员')),
    enabled INTEGER NOT NULL DEFAULT 1 CHECK (enabled IN (0,1))
);

CREATE TABLE IF NOT EXISTS check_plan (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    plan_date    TEXT    NOT NULL,
    equipment_id INTEGER NOT NULL REFERENCES equipment(id),
    status       TEXT    NOT NULL DEFAULT '待点检' CHECK (status IN ('待点检','已完成')),
    created_at   TEXT    NOT NULL,
    UNIQUE (plan_date, equipment_id)
);

CREATE TABLE IF NOT EXISTS check_record (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    plan_id       INTEGER NOT NULL REFERENCES check_plan(id) ON DELETE CASCADE,
    check_item_id INTEGER NOT NULL REFERENCES check_item(id),
    value         REAL    NOT NULL,
    judged        TEXT    NOT NULL CHECK (judged IN ('正常','异常')),
    note          TEXT    NOT NULL DEFAULT '',
    operator_id   INTEGER REFERENCES operator(id),
    checked_at    TEXT    NOT NULL,
    UNIQUE (plan_id, check_item_id)
);

CREATE TABLE IF NOT EXISTS work_order (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    order_no      TEXT    NOT NULL UNIQUE,
    equipment_id  INTEGER NOT NULL REFERENCES equipment(id),
    record_id     INTEGER REFERENCES check_record(id) ON DELETE SET NULL,
    check_item_id INTEGER REFERENCES check_item(id) ON DELETE SET NULL,
    level         TEXT    NOT NULL CHECK (level IN ('一般','紧急')),
    status        TEXT    NOT NULL DEFAULT '待处理' CHECK (status IN ('待处理','处理中','已完成','已关闭')),
    description   TEXT    NOT NULL DEFAULT '',
    assignee      TEXT    NOT NULL DEFAULT '',
    solution      TEXT    NOT NULL DEFAULT '',
    created_at    TEXT    NOT NULL,
    started_at    TEXT    NOT NULL DEFAULT '',
    closed_at     TEXT    NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS audit_log (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    action     TEXT NOT NULL,
    entity     TEXT NOT NULL DEFAULT '',
    entity_id  INTEGER,
    detail     TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_plan_date    ON check_plan (plan_date);
CREATE INDEX IF NOT EXISTS idx_plan_status  ON check_plan (status);
CREATE INDEX IF NOT EXISTS idx_record_plan  ON check_record (plan_id);
CREATE INDEX IF NOT EXISTS idx_record_item  ON check_record (check_item_id, judged);
CREATE INDEX IF NOT EXISTS idx_order_status ON work_order (status);
CREATE INDEX IF NOT EXISTS idx_order_item   ON work_order (check_item_id, status);

CREATE VIEW IF NOT EXISTS v_equipment_overview AS
SELECT e.id,
       e.code,
       e.name,
       e.status,
       (SELECT COUNT(*) FROM check_item i WHERE i.equipment_id = e.id AND i.enabled = 1) AS item_count,
       (SELECT COUNT(*) FROM work_order w WHERE w.equipment_id = e.id AND w.status IN ('待处理','处理中')) AS open_orders,
       IFNULL((SELECT MAX(p.plan_date) FROM check_plan p WHERE p.equipment_id = e.id AND p.status = '已完成'), '') AS last_check
FROM equipment e;
";

        static void EnsureSchema()
        {
            db.Exec(Schema);
        }

        // ---------- 种子数据 ----------

        static void Seed()
        {
            DateTime today = DateTime.Today;
            DateTime yesterday = today.AddDays(-1);

            var equipmentIds = new Dictionary<string, long>();
            var itemIds = new Dictionary<string, long>();
            long opZhang, opLi;

            db.Begin();
            try
            {
                equipmentIds["EQ-001"] = InsertEquipment("EQ-001", "数控车床", "CK6150", "机加工一车间 A 区", StatusInUse, "2021-03-15", "两班制，重点设备");
                equipmentIds["EQ-002"] = InsertEquipment("EQ-002", "立式加工中心", "VMC850", "机加工一车间 A 区", StatusInUse, "2022-06-08", "承担模具型腔加工");
                equipmentIds["EQ-003"] = InsertEquipment("EQ-003", "外圆磨床", "M1432", "精加工区 B 区", StatusInUse, "2020-09-20", "砂轮主轴为关键部位");
                equipmentIds["EQ-004"] = InsertEquipment("EQ-004", "超声波清洗机", "QX-200", "清洗区", StatusInUse, "2023-01-10", "");
                equipmentIds["EQ-005"] = InsertEquipment("EQ-005", "螺杆空压机", "GA-37", "动力站", StatusInUse, "2019-11-05", "为全厂提供压缩空气");
                equipmentIds["EQ-006"] = InsertEquipment("EQ-006", "三坐标测量机", "CMM-500", "计量室", StatusStopped, "2018-05-12", "停用待搬迁");

                SeedItems(equipmentIds["EQ-001"], new[] {
                    new[] { "主轴温度", "主轴", "°C", "20", "75", "测温", "58" },
                    new[] { "主轴振动", "主轴", "mm/s", "0", "4.5", "测振", "2.1" },
                    new[] { "润滑油位", "润滑站", "%", "30", "100", "目视", "72" },
                    new[] { "卡盘压力", "液压站", "MPa", "0.4", "0.8", "测压", "0.62" }
                }, itemIds);
                SeedItems(equipmentIds["EQ-002"], new[] {
                    new[] { "主轴温度", "主轴", "°C", "20", "70", "测温", "61" },
                    new[] { "刀库气压", "气动柜", "MPa", "0.5", "0.7", "测压", "0.62" },
                    new[] { "冷却液液位", "水箱", "%", "25", "100", "目视", "66" },
                    new[] { "导轨润滑", "润滑站", "%", "20", "100", "目视", "54" }
                }, itemIds);
                SeedItems(equipmentIds["EQ-003"], new[] {
                    new[] { "砂轮转速", "砂轮主轴", "r/min", "1400", "1600", "测速", "1502" },
                    new[] { "主轴温度", "主轴", "°C", "20", "65", "测温", "47" },
                    new[] { "冷却液流量", "冷却系统", "L/min", "8", "12", "测流", "9.6" }
                }, itemIds);
                SeedItems(equipmentIds["EQ-004"], new[] {
                    new[] { "清洗液温度", "清洗槽", "°C", "40", "60", "测温", "51" },
                    new[] { "泵压力", "泵站", "MPa", "0.2", "0.5", "测压", "0.36" }
                }, itemIds);
                SeedItems(equipmentIds["EQ-005"], new[] {
                    new[] { "排气压力", "储气罐", "MPa", "0.6", "0.8", "测压", "0.71" },
                    new[] { "排气温度", "主机", "°C", "0", "95", "测温", "78" },
                    new[] { "油位", "油箱", "%", "30", "100", "目视", "63" }
                }, itemIds);
                SeedItems(equipmentIds["EQ-006"], new[] {
                    new[] { "环境温度", "计量室", "°C", "18", "24", "测温", "21" },
                    new[] { "相对湿度", "计量室", "%", "40", "60", "目视", "48" }
                }, itemIds);

                opZhang = InsertOperator("张伟", "点检员");
                opLi = InsertOperator("李静", "点检员");
                InsertOperator("王强", "维修工");
                InsertOperator("陈敏", "管理员");

                // 昨天的计划全部完成，其中 EQ-003 砂轮转速超上限，产生一张处理中的紧急工单。
                string yesterdayText = Date(yesterday);
                var yesterdayValues = new Dictionary<string, string>();
                foreach (var code in new[] { "EQ-001", "EQ-002", "EQ-003", "EQ-004", "EQ-005" })
                    foreach (var item in itemIds)
                        if (item.Key.StartsWith(code + "|", StringComparison.Ordinal))
                            yesterdayValues[item.Key] = NormalValue(item.Key);
                yesterdayValues["EQ-003|砂轮转速"] = "1655";

                var yesterdayPlans = new Dictionary<string, long>();
                foreach (var code in new[] { "EQ-001", "EQ-002", "EQ-003", "EQ-004", "EQ-005" })
                {
                    long planId = db.Insert(
                        "INSERT INTO check_plan (plan_date, equipment_id, status, created_at) VALUES (?,?,?,?)",
                        yesterdayText, equipmentIds[code], PlanDone, yesterdayText + " 07:50:00");
                    yesterdayPlans[code] = planId;
                }

                long badRecord = 0;
                int minutes = 0;
                foreach (var pair in yesterdayValues)
                {
                    string[] key = pair.Key.Split('|');
                    string code = key[0];
                    string itemName = key[1];
                    var item = GetItemRow(itemIds[pair.Key]);
                    string judged = Judge(item, double.Parse(pair.Value, CultureInfo.InvariantCulture));
                    long recordId = db.Insert(
                        "INSERT INTO check_record (plan_id, check_item_id, value, judged, note, operator_id, checked_at) VALUES (?,?,?,?,?,?,?)",
                        yesterdayPlans[code], item.Id, double.Parse(pair.Value, CultureInfo.InvariantCulture), judged,
                        judged == JudgeBad ? "读数偏高，已通知维修班" : "", opZhang,
                        yesterdayText + " 08:" + (30 + minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"));
                    minutes += 3;
                    if (code == "EQ-003" && itemName == "砂轮转速") badRecord = recordId;
                }

                db.Insert(
                    "INSERT INTO work_order (order_no, equipment_id, record_id, check_item_id, level, status, description, assignee, solution, created_at, started_at, closed_at) " +
                    "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
                    "WO-" + yesterday.ToString("yyyyMMdd") + "-001", equipmentIds["EQ-003"], badRecord,
                    itemIds["EQ-003|砂轮转速"], "紧急", OrderWorking,
                    "砂轮转速实测 1655 r/min，超出上限 1600 r/min，砂轮主轴存在过热风险。",
                    "王强", "", yesterdayText + " 08:55:00", yesterdayText + " 10:20:00", "");

                // 今天的计划：三张已完成（含一条异常），两张待点检。
                string todayText = Date(today);
                var todayValues = new Dictionary<string, string>();
                foreach (var item in itemIds) todayValues[item.Key] = NormalValue(item.Key);
                todayValues["EQ-002|刀库气压"] = "0.42";

                var todayPlans = new Dictionary<string, long>();
                foreach (var code in new[] { "EQ-001", "EQ-002", "EQ-003", "EQ-004", "EQ-005" })
                {
                    string status = code == "EQ-003" || code == "EQ-005" ? PlanPending : PlanDone;
                    todayPlans[code] = db.Insert(
                        "INSERT INTO check_plan (plan_date, equipment_id, status, created_at) VALUES (?,?,?,?)",
                        todayText, equipmentIds[code], status, todayText + " 07:50:00");
                }

                long badRecord2 = 0;
                minutes = 0;
                foreach (var code in new[] { "EQ-001", "EQ-002", "EQ-004" })
                {
                    foreach (var item in itemIds)
                    {
                        if (!item.Key.StartsWith(code + "|", StringComparison.Ordinal)) continue;
                        string valueText = todayValues[item.Key];
                        var row = GetItemRow(item.Value);
                        string judged = Judge(row, double.Parse(valueText, CultureInfo.InvariantCulture));
                        long recordId = db.Insert(
                            "INSERT INTO check_record (plan_id, check_item_id, value, judged, note, operator_id, checked_at) VALUES (?,?,?,?,?,?,?)",
                            todayPlans[code], row.Id, double.Parse(valueText, CultureInfo.InvariantCulture), judged,
                            judged == JudgeBad ? "低于下限，已通知维修班" : "", opLi,
                            todayText + " 08:" + (20 + minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"));
                        minutes += 3;
                        if (code == "EQ-002" && item.Key == "EQ-002|刀库气压") badRecord2 = recordId;
                    }
                }

                db.Insert(
                    "INSERT INTO work_order (order_no, equipment_id, record_id, check_item_id, level, status, description, assignee, solution, created_at, started_at, closed_at) " +
                    "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
                    "WO-" + today.ToString("yyyyMMdd") + "-001", equipmentIds["EQ-002"], badRecord2,
                    itemIds["EQ-002|刀库气压"], "紧急", OrderPending,
                    "刀库气压实测 0.42 MPa，低于下限 0.5 MPa，换刀动作可能不到位。",
                    "", "", todayText + " 08:26:00", "", "");

                // 一张历史已关闭工单，用于展示关闭率。
                DateTime oldDate = today.AddDays(-3);
                db.Insert(
                    "INSERT INTO work_order (order_no, equipment_id, record_id, check_item_id, level, status, description, assignee, solution, created_at, started_at, closed_at) " +
                    "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
                    "WO-" + oldDate.ToString("yyyyMMdd") + "-001", equipmentIds["EQ-001"], null, null, "一般", OrderClosed,
                    "主轴异响，振动值偏高。", "王强", "更换主轴前轴承并重新动平衡，复测振动 2.3 mm/s。",
                    Date(oldDate) + " 09:10:00", Date(oldDate) + " 09:30:00", Date(today.AddDays(-2)) + " 16:40:00");

                Log("初始化", "数据库", 0, "写入示例：6 台设备、18 个点检项、4 名人员、10 张计划、26 条记录、3 张工单。");
                db.Commit();
            }
            catch
            {
                db.Rollback();
                throw;
            }
        }

        static void SeedItems(long equipmentId, string[][] items, Dictionary<string, long> itemIds)
        {
            string code = db.Query("SELECT code AS c FROM equipment WHERE id=?", equipmentId)[0].Str("c");
            foreach (var item in items)
            {
                long id = db.Insert(
                    "INSERT INTO check_item (equipment_id, name, part, unit, lower, upper, method, enabled) VALUES (?,?,?,?,?,?,?,1)",
                    equipmentId, item[0], item[1], item[2],
                    double.Parse(item[3], CultureInfo.InvariantCulture), double.Parse(item[4], CultureInfo.InvariantCulture),
                    item[5]);
                itemIds[code + "|" + item[0]] = id;
            }
        }

        static string NormalValue(string key)
        {
            // 种子数据中每个点检项的基准读数，与 SeedItems 的最后一列一致。
            switch (key)
            {
                case "EQ-001|主轴温度": return "58";
                case "EQ-001|主轴振动": return "2.1";
                case "EQ-001|润滑油位": return "72";
                case "EQ-001|卡盘压力": return "0.62";
                case "EQ-002|主轴温度": return "61";
                case "EQ-002|刀库气压": return "0.62";
                case "EQ-002|冷却液液位": return "66";
                case "EQ-002|导轨润滑": return "54";
                case "EQ-003|砂轮转速": return "1502";
                case "EQ-003|主轴温度": return "47";
                case "EQ-003|冷却液流量": return "9.6";
                case "EQ-004|清洗液温度": return "51";
                case "EQ-004|泵压力": return "0.36";
                case "EQ-005|排气压力": return "0.71";
                case "EQ-005|排气温度": return "78";
                case "EQ-005|油位": return "63";
                case "EQ-006|环境温度": return "21";
                case "EQ-006|相对湿度": return "48";
            }
            return "0";
        }

        static long InsertEquipment(string code, string name, string model, string location, string status, string commissioned, string remark)
        {
            return db.Insert(
                "INSERT INTO equipment (code, name, model, location, status, commissioned_at, remark) VALUES (?,?,?,?,?,?,?)",
                code, name, model, location, status, commissioned, remark);
        }

        static long InsertOperator(string name, string role)
        {
            return db.Insert("INSERT INTO operator (name, role, enabled) VALUES (?,?,1)", name, role);
        }

        static CheckItemRow GetItemRow(long id)
        {
            var rows = db.Query("SELECT id, equipment_id, name, part, unit, lower, upper, method, enabled FROM check_item WHERE id=?", id);
            if (rows.Count == 0) throw new InvalidOperationException("点检项不存在：" + id);
            return MapItem(rows[0]);
        }

        static CheckItemRow MapItem(Row row)
        {
            var item = new CheckItemRow();
            item.Id = row.Int("id");
            item.EquipmentId = row.Int("equipment_id");
            item.Name = row.Str("name");
            item.Part = row.Str("part");
            item.Unit = row.Str("unit");
            item.Lower = row.Dbl("lower");
            item.Upper = row.Dbl("upper");
            item.Method = row.Str("method");
            item.Enabled = row.Int("enabled") == 1;
            return item;
        }

        // ---------- 通用 ----------

        static long Count(string sql, params object[] args)
        {
            var rows = db.Query(sql, args);
            return rows.Count == 0 ? 0 : rows[0].Int("n");
        }

        static string Date(DateTime value) { return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        static string Now() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }
        static string Num(double value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }

        public static void Log(string action, string entity, long entityId, string detail)
        {
            db.Insert("INSERT INTO audit_log (action, entity, entity_id, detail, created_at) VALUES (?,?,?,?,?)",
                action, entity, entityId, detail, Now());
        }

        // ---------- 设备台账 ----------

        public static List<EquipmentRow> GetEquipments(string keyword, string status)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT e.id, e.code, e.name, e.model, e.location, e.status, e.commissioned_at, e.remark, ");
            sql.Append("(SELECT COUNT(*) FROM check_item i WHERE i.equipment_id=e.id AND i.enabled=1) AS item_count, ");
            sql.Append("(SELECT COUNT(*) FROM work_order w WHERE w.equipment_id=e.id AND w.status IN ('待处理','处理中')) AS open_orders, ");
            sql.Append("IFNULL((SELECT MAX(p.plan_date) FROM check_plan p WHERE p.equipment_id=e.id AND p.status='已完成'),'') AS last_check ");
            sql.Append("FROM equipment e WHERE 1=1");
            var args = new List<object>();
            if (!string.IsNullOrEmpty(keyword))
            {
                sql.Append(" AND (e.code LIKE ? OR e.name LIKE ? OR e.location LIKE ?)");
                args.Add("%" + keyword + "%");
                args.Add("%" + keyword + "%");
                args.Add("%" + keyword + "%");
            }
            if (!string.IsNullOrEmpty(status) && status != "全部")
            {
                sql.Append(" AND e.status=?");
                args.Add(status);
            }
            sql.Append(" ORDER BY e.code");

            var list = new List<EquipmentRow>();
            foreach (var row in db.Query(sql.ToString(), args.ToArray()))
            {
                var e = new EquipmentRow();
                e.Id = row.Int("id");
                e.Code = row.Str("code");
                e.Name = row.Str("name");
                e.Model = row.Str("model");
                e.Location = row.Str("location");
                e.Status = row.Str("status");
                e.CommissionedAt = row.Str("commissioned_at");
                e.Remark = row.Str("remark");
                e.ItemCount = (int)row.Int("item_count");
                e.OpenOrders = (int)row.Int("open_orders");
                e.LastCheck = row.Str("last_check");
                list.Add(e);
            }
            return list;
        }

        public static EquipmentRow GetEquipment(long id)
        {
            foreach (var e in GetEquipments("", "全部"))
                if (e.Id == id) return e;
            return null;
        }

        public static long SaveEquipment(EquipmentRow e)
        {
            if (string.IsNullOrEmpty(e.Code)) throw new InvalidOperationException("设备编号不能为空。");
            if (string.IsNullOrEmpty(e.Name)) throw new InvalidOperationException("设备名称不能为空。");
            if (e.Id == 0)
            {
                long id = db.Insert(
                    "INSERT INTO equipment (code, name, model, location, status, commissioned_at, remark) VALUES (?,?,?,?,?,?,?)",
                    e.Code, e.Name, e.Model, e.Location, e.Status, e.CommissionedAt, e.Remark);
                Log("新增", "设备", id, e.Code + " " + e.Name);
                return id;
            }
            db.Execute(
                "UPDATE equipment SET code=?, name=?, model=?, location=?, status=?, commissioned_at=?, remark=? WHERE id=?",
                e.Code, e.Name, e.Model, e.Location, e.Status, e.CommissionedAt, e.Remark, e.Id);
            Log("修改", "设备", e.Id, e.Code + " " + e.Name);
            return e.Id;
        }

        public static bool DeleteEquipment(long id)
        {
            long used = Count("SELECT (SELECT COUNT(*) FROM check_plan WHERE equipment_id=?) + (SELECT COUNT(*) FROM work_order WHERE equipment_id=?) AS n", id, id);
            if (used > 0) return false;
            var rows = db.Query("SELECT code AS c FROM equipment WHERE id=?", id);
            db.Execute("DELETE FROM equipment WHERE id=?", id);
            if (rows.Count > 0) Log("删除", "设备", id, rows[0].Str("c"));
            return true;
        }

        public static void SetEquipmentStatus(long id, string status)
        {
            db.Execute("UPDATE equipment SET status=? WHERE id=?", status, id);
            Log("改状态", "设备", id, status);
        }

        // ---------- 点检标准 ----------

        public static List<CheckItemRow> GetItems(long equipmentId)
        {
            var list = new List<CheckItemRow>();
            foreach (var row in db.Query("SELECT id, equipment_id, name, part, unit, lower, upper, method, enabled FROM check_item WHERE equipment_id=? ORDER BY id", equipmentId))
                list.Add(MapItem(row));
            return list;
        }

        public static long SaveItem(CheckItemRow item)
        {
            if (string.IsNullOrEmpty(item.Name)) throw new InvalidOperationException("点检项目名称不能为空。");
            if (item.Lower > item.Upper) throw new InvalidOperationException("下限不能大于上限。");
            if (item.Id == 0)
            {
                long id = db.Insert(
                    "INSERT INTO check_item (equipment_id, name, part, unit, lower, upper, method, enabled) VALUES (?,?,?,?,?,?,?,?)",
                    item.EquipmentId, item.Name, item.Part, item.Unit, item.Lower, item.Upper, item.Method, item.Enabled ? 1 : 0);
                Log("新增", "点检项", id, item.Name);
                return id;
            }
            db.Execute(
                "UPDATE check_item SET name=?, part=?, unit=?, lower=?, upper=?, method=?, enabled=? WHERE id=?",
                item.Name, item.Part, item.Unit, item.Lower, item.Upper, item.Method, item.Enabled ? 1 : 0, item.Id);
            Log("修改", "点检项", item.Id, item.Name);
            return item.Id;
        }

        public static void DeleteItem(long id)
        {
            long used = Count("SELECT COUNT(*) AS n FROM check_record WHERE check_item_id=?", id);
            if (used > 0) throw new InvalidOperationException("该项目已有点检记录，不能删除，可将其停用。");
            db.Execute("DELETE FROM check_item WHERE id=?", id);
            Log("删除", "点检项", id, "");
        }

        public static void SetItemEnabled(long id, bool enabled)
        {
            db.Execute("UPDATE check_item SET enabled=? WHERE id=?", enabled ? 1 : 0, id);
            Log(enabled ? "启用" : "停用", "点检项", id, "");
        }

        public static List<OperatorRow> GetOperators()
        {
            var list = new List<OperatorRow>();
            foreach (var row in db.Query("SELECT id, name, role FROM operator WHERE enabled=1 ORDER BY id"))
            {
                var op = new OperatorRow();
                op.Id = row.Int("id");
                op.Name = row.Str("name");
                op.Role = row.Str("role");
                list.Add(op);
            }
            return list;
        }

        // ---------- 点检计划与记录 ----------

        public static int GeneratePlans(DateTime date)
        {
            db.Begin();
            try
            {
                int created = GeneratePlansCore(date);
                db.Commit();
                if (created > 0) Log("生成计划", "点检计划", 0, Date(date) + " 新增 " + created + " 张");
                return created;
            }
            catch
            {
                db.Rollback();
                throw;
            }
        }

        static int GeneratePlansCore(DateTime date)
        {
            int created = 0;
            string text = Date(date);
            foreach (var row in db.Query("SELECT id FROM equipment WHERE status='在用' ORDER BY code"))
            {
                long equipmentId = row.Int("id");
                long items = Count("SELECT COUNT(*) AS n FROM check_item WHERE equipment_id=? AND enabled=1", equipmentId);
                if (items == 0) continue;
                long exists = Count("SELECT COUNT(*) AS n FROM check_plan WHERE plan_date=? AND equipment_id=?", text, equipmentId);
                if (exists > 0) continue;
                db.Insert("INSERT INTO check_plan (plan_date, equipment_id, status, created_at) VALUES (?,?,?,?)",
                    text, equipmentId, PlanPending, Now());
                created++;
            }
            return created;
        }

        public static List<PlanRow> GetPlans(DateTime date, string status)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT p.id, p.equipment_id, p.plan_date, e.code, e.name, p.status, ");
            sql.Append("(SELECT COUNT(*) FROM check_item i WHERE i.equipment_id=e.id AND i.enabled=1) AS item_count, ");
            sql.Append("(SELECT COUNT(*) FROM check_record r WHERE r.plan_id=p.id) AS done_count, ");
            sql.Append("(SELECT COUNT(*) FROM check_record r WHERE r.plan_id=p.id AND r.judged='异常') AS abnormal_count ");
            sql.Append("FROM check_plan p JOIN equipment e ON e.id=p.equipment_id WHERE p.plan_date=?");
            var args = new List<object> { Date(date) };
            if (!string.IsNullOrEmpty(status) && status != "全部")
            {
                sql.Append(" AND p.status=?");
                args.Add(status);
            }
            sql.Append(" ORDER BY e.code");

            var list = new List<PlanRow>();
            foreach (var row in db.Query(sql.ToString(), args.ToArray()))
            {
                var p = new PlanRow();
                p.Id = row.Int("id");
                p.EquipmentId = row.Int("equipment_id");
                p.PlanDate = row.Str("plan_date");
                p.Code = row.Str("code");
                p.Name = row.Str("name");
                p.Status = row.Str("status");
                p.ItemCount = (int)row.Int("item_count");
                p.DoneCount = (int)row.Int("done_count");
                p.AbnormalCount = (int)row.Int("abnormal_count");
                list.Add(p);
            }
            return list;
        }

        public static PlanRow GetPlan(long planId)
        {
            var rows = db.Query("SELECT plan_date, status FROM check_plan WHERE id=?", planId);
            if (rows.Count == 0) return null;
            var p = new PlanRow();
            p.Id = planId;
            p.PlanDate = rows[0].Str("plan_date");
            p.Status = rows[0].Str("status");
            return p;
        }

        public static List<RecordRow> GetPlanRecords(long planId)
        {
            var list = new List<RecordRow>();
            string sql =
                "SELECT i.id AS item_id, i.name, i.part, i.unit, i.lower, i.upper, i.method, " +
                "r.value, IFNULL(r.judged,'') AS judged, IFNULL(r.note,'') AS note, IFNULL(r.checked_at,'') AS checked_at " +
                "FROM check_item i JOIN check_plan p ON p.equipment_id=i.equipment_id AND p.id=? " +
                "LEFT JOIN check_record r ON r.plan_id=p.id AND r.check_item_id=i.id " +
                "WHERE i.enabled=1 ORDER BY i.id";
            foreach (var row in db.Query(sql, planId))
            {
                var rec = new RecordRow();
                rec.ItemId = row.Int("item_id");
                rec.Name = row.Str("name");
                rec.Part = row.Str("part");
                rec.Unit = row.Str("unit");
                rec.Lower = row.Dbl("lower");
                rec.Upper = row.Dbl("upper");
                rec.Method = row.Str("method");
                rec.HasValue = !row.IsNull("value");
                rec.Value = row.Dbl("value");
                rec.Judged = row.Str("judged");
                rec.Note = row.Str("note");
                rec.CheckedAt = row.Str("checked_at");
                list.Add(rec);
            }
            return list;
        }

        public static string Judge(CheckItemRow item, double value)
        {
            return value < item.Lower || value > item.Upper ? JudgeBad : JudgeOk;
        }

        public static bool IsUrgent(CheckItemRow item, double value)
        {
            double span = item.Upper - item.Lower;
            if (span <= 0) span = Math.Max(Math.Abs(item.Upper), 1) * 0.1;
            double margin = span * 0.2;
            return value < item.Lower - margin || value > item.Upper + margin;
        }

        /// <summary>保存一条点检读数；异常时自动开单，并刷新计划完成状态。</summary>
        public static void SaveRecord(long planId, long itemId, double value, string note, long operatorId)
        {
            double.Parse(value.ToString("0.###", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            CheckItemRow item = GetItemRow(itemId);
            PlanRow plan = GetPlan(planId);
            if (plan == null) throw new InvalidOperationException("点检计划不存在。");
            if (plan.Status == PlanDone) throw new InvalidOperationException("该计划已完成，不能修改读数。");

            string judged = Judge(item, value);
            db.Begin();
            try
            {
                long existing = Count("SELECT COUNT(*) AS n FROM check_record WHERE plan_id=? AND check_item_id=?", planId, itemId);
                if (existing == 0)
                {
                    db.Insert(
                        "INSERT INTO check_record (plan_id, check_item_id, value, judged, note, operator_id, checked_at) VALUES (?,?,?,?,?,?,?)",
                        planId, itemId, value, judged, note, operatorId, Now());
                }
                else
                {
                    db.Execute(
                        "UPDATE check_record SET value=?, judged=?, note=?, operator_id=?, checked_at=? WHERE plan_id=? AND check_item_id=?",
                        value, judged, note, operatorId, Now(), planId, itemId);
                }

                if (judged == JudgeBad) CreateOrderForRecord(planId, item, value);

                long total = Count("SELECT COUNT(*) AS n FROM check_item WHERE equipment_id=? AND enabled=1", item.EquipmentId);
                long done = Count("SELECT COUNT(*) AS n FROM check_record WHERE plan_id=?", planId);
                db.Execute("UPDATE check_plan SET status=? WHERE id=?", done >= total ? PlanDone : PlanPending, planId);
                db.Commit();
            }
            catch
            {
                db.Rollback();
                throw;
            }

            Log("录入", "点检记录", itemId, item.Name + " " + Num(value) + item.Unit + " → " + judged);
        }

        static void CreateOrderForRecord(long planId, CheckItemRow item, double value)
        {
            long open = Count("SELECT COUNT(*) AS n FROM work_order WHERE check_item_id=? AND status IN ('待处理','处理中')", item.Id);
            if (open > 0) return;

            long recordId = Count("SELECT COUNT(*) AS n FROM check_record WHERE plan_id=? AND check_item_id=?", planId, item.Id) > 0
                ? db.Query("SELECT id FROM check_record WHERE plan_id=? AND check_item_id=?", planId, item.Id)[0].Int("id")
                : 0;

            string level = IsUrgent(item, value) ? "紧急" : "一般";
            string description = item.Name + "实测 " + Num(value) + item.Unit +
                "，超出限值 [" + Num(item.Lower) + ", " + Num(item.Upper) + "] " + item.Unit + "。";
            long orderId = db.Insert(
                "INSERT INTO work_order (order_no, equipment_id, record_id, check_item_id, level, status, description, assignee, solution, created_at, started_at, closed_at) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
                NextOrderNo(DateTime.Today), item.EquipmentId, recordId, item.Id, level, OrderPending, description, "", "", Now(), "", "");
            Log("自动开单", "工单", orderId, description);
        }

        static string NextOrderNo(DateTime date)
        {
            string prefix = "WO-" + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";
            long used = Count("SELECT COUNT(*) AS n FROM work_order WHERE order_no LIKE ?", prefix + "%");
            return prefix + (used + 1).ToString("000", CultureInfo.InvariantCulture);
        }

        // ---------- 维修工单 ----------

        public static List<OrderRow> GetOrders(string status)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT w.id, w.order_no, w.equipment_id, e.code, e.name, w.level, w.status, w.description, w.assignee, w.solution, w.created_at, w.started_at, w.closed_at ");
            sql.Append("FROM work_order w JOIN equipment e ON e.id=w.equipment_id WHERE 1=1");
            var args = new List<object>();
            if (!string.IsNullOrEmpty(status) && status != "全部")
            {
                sql.Append(" AND w.status=?");
                args.Add(status);
            }
            sql.Append(" ORDER BY CASE w.status WHEN '待处理' THEN 0 WHEN '处理中' THEN 1 WHEN '已完成' THEN 2 ELSE 3 END, w.created_at DESC");

            var list = new List<OrderRow>();
            foreach (var row in db.Query(sql.ToString(), args.ToArray()))
            {
                var o = new OrderRow();
                o.Id = row.Int("id");
                o.OrderNo = row.Str("order_no");
                o.EquipmentId = row.Int("equipment_id");
                o.Code = row.Str("code");
                o.Name = row.Str("name");
                o.Level = row.Str("level");
                o.Status = row.Str("status");
                o.Description = row.Str("description");
                o.Assignee = row.Str("assignee");
                o.Solution = row.Str("solution");
                o.CreatedAt = row.Str("created_at");
                o.StartedAt = row.Str("started_at");
                o.ClosedAt = row.Str("closed_at");
                list.Add(o);
            }
            return list;
        }

        public static OrderRow GetOrder(long id)
        {
            foreach (var o in GetOrders("全部"))
                if (o.Id == id) return o;
            return null;
        }

        public static void StartOrder(long id, string assignee)
        {
            if (string.IsNullOrEmpty(assignee)) throw new InvalidOperationException("请填写处理人。");
            int changed = db.Execute("UPDATE work_order SET status=?, assignee=?, started_at=? WHERE id=? AND status=?",
                OrderWorking, assignee, Now(), id, OrderPending);
            if (changed == 0) throw new InvalidOperationException("只有“待处理”的工单可以开始处理。");
            Log("开始处理", "工单", id, assignee);
        }

        public static void CompleteOrder(long id, string solution)
        {
            if (string.IsNullOrEmpty(solution)) throw new InvalidOperationException("完成工单前必须填写处理措施。");
            int changed = db.Execute("UPDATE work_order SET status=?, solution=? WHERE id=? AND status=?",
                OrderFinished, solution, id, OrderWorking);
            if (changed == 0) throw new InvalidOperationException("只有“处理中”的工单可以完成。");
            Log("完成", "工单", id, solution);
        }

        public static void CloseOrder(long id)
        {
            int changed = db.Execute("UPDATE work_order SET status=?, closed_at=? WHERE id=? AND status=?",
                OrderClosed, Now(), id, OrderFinished);
            if (changed == 0) throw new InvalidOperationException("只有“已完成”的工单可以关闭。");
            Log("关闭", "工单", id, "");
        }

        // ---------- 统计 ----------

        public static Stats ComputeStats(DateTime from, DateTime to)
        {
            if (from > to) throw new InvalidOperationException("起始日期不能晚于结束日期。");
            string start = Date(from);
            string end = Date(to);

            var stats = new Stats();
            stats.From = from;
            stats.To = to;
            stats.Equipments = (int)Count("SELECT COUNT(*) AS n FROM equipment WHERE status='在用'");
            stats.Plans = (int)Count("SELECT COUNT(*) AS n FROM check_plan WHERE plan_date BETWEEN ? AND ?", start, end);
            stats.DonePlans = (int)Count("SELECT COUNT(*) AS n FROM check_plan WHERE plan_date BETWEEN ? AND ? AND status='已完成'", start, end);
            stats.Records = (int)Count("SELECT COUNT(*) AS n FROM check_record r JOIN check_plan p ON p.id=r.plan_id WHERE p.plan_date BETWEEN ? AND ?", start, end);
            stats.Abnormal = (int)Count("SELECT COUNT(*) AS n FROM check_record r JOIN check_plan p ON p.id=r.plan_id WHERE p.plan_date BETWEEN ? AND ? AND r.judged='异常'", start, end);
            stats.OpenOrders = (int)Count("SELECT COUNT(*) AS n FROM work_order WHERE status IN ('待处理','处理中')");
            stats.ClosedOrders = (int)Count("SELECT COUNT(*) AS n FROM work_order WHERE status='已关闭'");
            stats.EquipmentsWithOpen = (int)Count("SELECT COUNT(DISTINCT equipment_id) AS n FROM work_order WHERE status IN ('待处理','处理中')");

            string byEquipment =
                "SELECT e.code, e.name, " +
                "(SELECT COUNT(*) FROM check_plan p WHERE p.equipment_id=e.id AND p.plan_date BETWEEN ?1 AND ?2) AS plans, " +
                "(SELECT COUNT(*) FROM check_plan p WHERE p.equipment_id=e.id AND p.plan_date BETWEEN ?1 AND ?2 AND p.status='已完成') AS done, " +
                "(SELECT COUNT(*) FROM check_record r JOIN check_plan p ON p.id=r.plan_id WHERE p.equipment_id=e.id AND p.plan_date BETWEEN ?1 AND ?2) AS records, " +
                "(SELECT COUNT(*) FROM check_record r JOIN check_plan p ON p.id=r.plan_id WHERE p.equipment_id=e.id AND p.plan_date BETWEEN ?1 AND ?2 AND r.judged='异常') AS abnormal, " +
                "(SELECT COUNT(*) FROM work_order w WHERE w.equipment_id=e.id AND w.status IN ('待处理','处理中')) AS open_orders, " +
                "IFNULL((SELECT MAX(p.plan_date) FROM check_plan p WHERE p.equipment_id=e.id AND p.status='已完成'),'') AS last_check " +
                "FROM equipment e ORDER BY e.code";
            foreach (var row in db.Query(byEquipment, start, end))
            {
                var item = new EquipStat();
                item.Code = row.Str("code");
                item.Name = row.Str("name");
                item.Plans = (int)row.Int("plans");
                item.Done = (int)row.Int("done");
                item.Records = (int)row.Int("records");
                item.Abnormal = (int)row.Int("abnormal");
                item.OpenOrders = (int)row.Int("open_orders");
                item.LastCheck = row.Str("last_check");
                stats.ByEquipment.Add(item);
            }

            string topAbnormal =
                "SELECT i.name AS item_name, e.code, e.name AS equipment_name, COUNT(*) AS n " +
                "FROM check_record r JOIN check_item i ON i.id=r.check_item_id " +
                "JOIN check_plan p ON p.id=r.plan_id JOIN equipment e ON e.id=p.equipment_id " +
                "WHERE r.judged='异常' AND p.plan_date BETWEEN ? AND ? " +
                "GROUP BY i.id ORDER BY n DESC, i.name LIMIT 10";
            foreach (var row in db.Query(topAbnormal, start, end))
            {
                var item = new AbnormalStat();
                item.ItemName = row.Str("item_name");
                item.Code = row.Str("code");
                item.EquipmentName = row.Str("equipment_name");
                item.Count = (int)row.Int("n");
                stats.TopAbnormal.Add(item);
            }
            return stats;
        }

        public static List<AuditRow> GetAudit(int limit)
        {
            var list = new List<AuditRow>();
            foreach (var row in db.Query("SELECT id, action, entity, entity_id, detail, created_at FROM audit_log ORDER BY id DESC LIMIT ?", limit))
            {
                var a = new AuditRow();
                a.Id = row.Int("id");
                a.Action = row.Str("action");
                a.Entity = row.Str("entity");
                a.Detail = row.Str("detail");
                a.CreatedAt = row.Str("created_at");
                list.Add(a);
            }
            return list;
        }

        public static void ExportStats(string path, Stats stats)
        {
            var text = new StringBuilder();
            text.AppendLine("设备点检与异常工单管理系统,统计报表");
            text.AppendLine("统计区间," + Date(stats.From) + "," + Date(stats.To));
            text.AppendLine();
            text.AppendLine("指标,数值");
            text.AppendLine("在用在册设备," + stats.Equipments);
            text.AppendLine("点检计划," + stats.Plans);
            text.AppendLine("已完成计划," + stats.DonePlans);
            text.AppendLine("计划完成率(%)," + stats.CompletionRate.ToString("0.00", CultureInfo.InvariantCulture));
            text.AppendLine("点检记录," + stats.Records);
            text.AppendLine("异常记录," + stats.Abnormal);
            text.AppendLine("异常率(%)," + stats.AbnormalRate.ToString("0.00", CultureInfo.InvariantCulture));
            text.AppendLine("设备完好率(%)," + stats.HealthRate.ToString("0.00", CultureInfo.InvariantCulture));
            text.AppendLine("未关闭工单," + stats.OpenOrders);
            text.AppendLine("已关闭工单," + stats.ClosedOrders);
            text.AppendLine("工单关闭率(%)," + stats.CloseRate.ToString("0.00", CultureInfo.InvariantCulture));
            text.AppendLine();
            text.AppendLine("设备编号,设备名称,计划数,完成数,完成率(%),记录数,异常数,异常率(%),未关闭工单,最近点检");
            foreach (var e in stats.ByEquipment)
            {
                double completion = e.Plans == 0 ? 0 : e.Done * 100.0 / e.Plans;
                double abnormal = e.Records == 0 ? 0 : e.Abnormal * 100.0 / e.Records;
                text.AppendLine(string.Join(",", new[] {
                    Csv(e.Code), Csv(e.Name), e.Plans.ToString(CultureInfo.InvariantCulture),
                    e.Done.ToString(CultureInfo.InvariantCulture), completion.ToString("0.00", CultureInfo.InvariantCulture),
                    e.Records.ToString(CultureInfo.InvariantCulture), e.Abnormal.ToString(CultureInfo.InvariantCulture),
                    abnormal.ToString("0.00", CultureInfo.InvariantCulture), e.OpenOrders.ToString(CultureInfo.InvariantCulture),
                    Csv(e.LastCheck)
                }));
            }
            text.AppendLine();
            text.AppendLine("排名,点检项目,设备编号,设备名称,异常次数");
            for (int i = 0; i < stats.TopAbnormal.Count; i++)
            {
                var a = stats.TopAbnormal[i];
                text.AppendLine(string.Join(",", new[] {
                    (i + 1).ToString(CultureInfo.InvariantCulture), Csv(a.ItemName), Csv(a.Code), Csv(a.EquipmentName),
                    a.Count.ToString(CultureInfo.InvariantCulture)
                }));
            }
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
        }

        static string Csv(string value)
        {
            if (value == null) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}