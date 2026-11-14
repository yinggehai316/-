using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Inspection
{
    sealed class DbException : Exception
    {
        public DbException(string message) : base(message) { }
    }

    /// <summary>一行查询结果，按列名取值。</summary>
    sealed class Row
    {
        readonly Dictionary<string, object> values;
        public Row(Dictionary<string, object> values) { this.values = values; }

        object Get(string name)
        {
            object value;
            if (!values.TryGetValue(name, out value))
                throw new ArgumentException("结果集中不存在列：" + name);
            return value;
        }

        public bool IsNull(string name) { return Get(name) == null; }

        public string Str(string name)
        {
            object value = Get(name);
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public long Int(string name)
        {
            object value = Get(name);
            return value == null ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        public double Dbl(string name)
        {
            object value = Get(name);
            return value == null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 对 Windows 自带 winsqlite3.dll 的薄封装。
    /// 只暴露程序实际用到的接口：执行脚本、参数化增删改、查询、事务。
    /// </summary>
    sealed class Db : IDisposable
    {
        const int Ok = 0;
        const int RowCode = 100;
        const int DoneCode = 101;
        const int OpenReadWrite = 2;
        const int OpenCreate = 4;

        static readonly IntPtr Transient = new IntPtr(-1);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_close(IntPtr database);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr sqlite3_errmsg(IntPtr database);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_exec(IntPtr database, byte[] sql, IntPtr callback, IntPtr argument, out IntPtr error);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern void sqlite3_free(IntPtr pointer);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_prepare_v2(IntPtr database, byte[] sql, int length, out IntPtr statement, IntPtr tail);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_finalize(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_step(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_reset(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_clear_bindings(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_bind_null(IntPtr statement, int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_bind_double(IntPtr statement, int index, double value);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int length, IntPtr destructor);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_column_count(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr sqlite3_column_name(IntPtr statement, int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_column_type(IntPtr statement, int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern long sqlite3_column_int64(IntPtr statement, int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern double sqlite3_column_double(IntPtr statement, int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr sqlite3_column_text(IntPtr statement, int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_column_bytes(IntPtr statement, int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern long sqlite3_last_insert_rowid(IntPtr database);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_changes(IntPtr database);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);

        IntPtr handle;
        bool disposed;

        public Db(string path)
        {
            byte[] name = Encoding.UTF8.GetBytes(path + "\0");
            IntPtr database;
            int code = sqlite3_open_v2(name, out database, OpenReadWrite | OpenCreate, IntPtr.Zero);
            if (code != Ok)
            {
                string detail = database == IntPtr.Zero ? "" : Utf8(sqlite3_errmsg(database));
                if (database != IntPtr.Zero) sqlite3_close(database);
                throw new DbException("无法打开数据库：" + detail);
            }
            handle = database;
            sqlite3_busy_timeout(handle, 3000);
            Exec("PRAGMA foreign_keys=ON;");
            Exec("PRAGMA journal_mode=DELETE;");
            Exec("PRAGMA synchronous=FULL;");
        }

        public string Path { get; private set; }

        public void Exec(string sql)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sql + "\0");
            IntPtr error;
            int code = sqlite3_exec(handle, bytes, IntPtr.Zero, IntPtr.Zero, out error);
            if (code != Ok)
            {
                string detail = error == IntPtr.Zero ? Utf8(sqlite3_errmsg(handle)) : Utf8(error);
                if (error != IntPtr.Zero) sqlite3_free(error);
                throw new DbException("SQL 执行失败：" + detail);
            }
        }

        public int Execute(string sql, params object[] args)
        {
            IntPtr statement = Prepare(sql);
            try
            {
                Bind(statement, args);
                int code = sqlite3_step(statement);
                if (code != DoneCode) throw Error("执行");
                return sqlite3_changes(handle);
            }
            finally
            {
                sqlite3_finalize(statement);
            }
        }

        public long Insert(string sql, params object[] args)
        {
            Execute(sql, args);
            return sqlite3_last_insert_rowid(handle);
        }

        public List<Row> Query(string sql, params object[] args)
        {
            IntPtr statement = Prepare(sql);
            var rows = new List<Row>();
            try
            {
                Bind(statement, args);
                int count = sqlite3_column_count(statement);
                string[] names = new string[count];
                for (int i = 0; i < count; i++)
                    names[i] = Utf8(sqlite3_column_name(statement, i));

                while (true)
                {
                    int code = sqlite3_step(statement);
                    if (code == RowCode)
                    {
                        var values = new Dictionary<string, object>(count);
                        for (int i = 0; i < count; i++)
                            values[names[i]] = ReadColumn(statement, i);
                        rows.Add(new Row(values));
                    }
                    else if (code == DoneCode)
                    {
                        break;
                    }
                    else
                    {
                        throw Error("查询");
                    }
                }
            }
            finally
            {
                sqlite3_finalize(statement);
            }
            return rows;
        }

        public void Begin() { Exec("BEGIN IMMEDIATE;"); }
        public void Commit() { Exec("COMMIT;"); }
        public void Rollback() { try { Exec("ROLLBACK;"); } catch (DbException) { } }

        IntPtr Prepare(string sql)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sql + "\0");
            IntPtr statement;
            int code = sqlite3_prepare_v2(handle, bytes, -1, out statement, IntPtr.Zero);
            if (code != Ok || statement == IntPtr.Zero) throw Error("准备语句");
            return statement;
        }

        void Bind(IntPtr statement, object[] args)
        {
            if (args == null) return;
            for (int i = 0; i < args.Length; i++)
            {
                object value = args[i];
                int index = i + 1;
                int code;
                if (value == null)
                {
                    code = sqlite3_bind_null(statement, index);
                }
                else if (value is string)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes((string)value);
                    code = sqlite3_bind_text(statement, index, bytes, bytes.Length, Transient);
                }
                else if (value is double || value is float || value is decimal)
                {
                    code = sqlite3_bind_double(statement, index, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                }
                else if (value is bool)
                {
                    code = sqlite3_bind_int64(statement, index, (bool)value ? 1 : 0);
                }
                else
                {
                    code = sqlite3_bind_int64(statement, index, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                }
                if (code != Ok) throw Error("绑定参数");
            }
        }

        object ReadColumn(IntPtr statement, int index)
        {
            int type = sqlite3_column_type(statement, index);
            if (type == 5) return null;
            if (type == 1) return sqlite3_column_int64(statement, index);
            if (type == 2) return sqlite3_column_double(statement, index);
            int length = sqlite3_column_bytes(statement, index);
            IntPtr pointer = sqlite3_column_text(statement, index);
            if (pointer == IntPtr.Zero || length <= 0) return "";
            var buffer = new byte[length];
            Marshal.Copy(pointer, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        DbException Error(string action)
        {
            return new DbException(action + "失败：" + Utf8(sqlite3_errmsg(handle)));
        }

        static string Utf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            var bytes = new List<byte>();
            for (int i = 0; ; i++)
            {
                byte value = Marshal.ReadByte(pointer, i);
                if (value == 0) break;
                bytes.Add(value);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (handle != IntPtr.Zero) sqlite3_close(handle);
            handle = IntPtr.Zero;
        }
    }
}