-- ============================================================
-- 设备点检与异常工单管理系统 · 数据库建库脚本
-- 数据库：SQLite 3（使用 Windows 自带 winsqlite3.dll）
-- 版本：V1.0
-- 说明：程序首次启动时会自动执行同一份 DDL 并写入示例数据；
--       本脚本用于通过命令行或第三方工具重建一个空库。
--       示例数据请直接使用 运行程序\数据库\inspection.db。
-- 用法：sqlite3 inspection.db < 建库脚本.sql
-- ============================================================

PRAGMA foreign_keys = ON;
PRAGMA user_version = 1;

-- 设备台账：一台设备一行，code 为业务编号
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

-- 点检标准：每台设备可配置多个点检项，lower/upper 构成合格区间
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

-- 人员：点检员 / 维修工 / 管理员，单机版不做登录，仅作为记录责任人
CREATE TABLE IF NOT EXISTS operator (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    name    TEXT    NOT NULL UNIQUE,
    role    TEXT    NOT NULL DEFAULT '点检员' CHECK (role IN ('点检员','维修工','管理员')),
    enabled INTEGER NOT NULL DEFAULT 1 CHECK (enabled IN (0,1))
);

-- 点检计划：每台在用设备每天一张，日期 + 设备唯一
CREATE TABLE IF NOT EXISTS check_plan (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    plan_date    TEXT    NOT NULL,
    equipment_id INTEGER NOT NULL REFERENCES equipment(id),
    status       TEXT    NOT NULL DEFAULT '待点检' CHECK (status IN ('待点检','已完成')),
    created_at   TEXT    NOT NULL,
    UNIQUE (plan_date, equipment_id)
);

-- 点检记录：一张计划下每个点检项最多一条记录，重复保存走更新
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

-- 维修工单：异常记录自动派生，状态机 待处理 → 处理中 → 已完成 → 已关闭
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

-- 操作日志：记录关键写操作，便于追溯
CREATE TABLE IF NOT EXISTS audit_log (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    action     TEXT NOT NULL,
    entity     TEXT NOT NULL DEFAULT '',
    entity_id  INTEGER,
    detail     TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL
);

-- 查询索引
CREATE INDEX IF NOT EXISTS idx_plan_date    ON check_plan (plan_date);
CREATE INDEX IF NOT EXISTS idx_plan_status  ON check_plan (status);
CREATE INDEX IF NOT EXISTS idx_record_plan  ON check_record (plan_id);
CREATE INDEX IF NOT EXISTS idx_record_item  ON check_record (check_item_id, judged);
CREATE INDEX IF NOT EXISTS idx_order_status ON work_order (status);
CREATE INDEX IF NOT EXISTS idx_order_item   ON work_order (check_item_id, status);

-- 设备总览视图：点检项数、未关闭工单数、最近一次完成点检日期
CREATE VIEW IF NOT EXISTS v_equipment_overview AS
SELECT e.id,
       e.code,
       e.name,
       e.status,
       (SELECT COUNT(*) FROM check_item i WHERE i.equipment_id = e.id AND i.enabled = 1) AS item_count,
       (SELECT COUNT(*) FROM work_order w WHERE w.equipment_id = e.id AND w.status IN ('待处理','处理中')) AS open_orders,
       IFNULL((SELECT MAX(p.plan_date) FROM check_plan p WHERE p.equipment_id = e.id AND p.status = '已完成'), '') AS last_check
FROM equipment e;