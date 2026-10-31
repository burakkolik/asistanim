using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace Asistanim
{
    public class Entry : INotifyPropertyChanged
    {
        private string _title = "";
        private string _detail = "";

        public string Title
        {
            get => _title;
            set { _title = (value ?? "").Replace("\r", "").Replace("\n", " "); Raise(nameof(Title)); }
        }

        public string Detail
        {
            get => _detail;
            set { _detail = (value ?? "").Replace("\r", ""); Raise(nameof(Detail)); Raise(nameof(Preview)); Raise(nameof(HasDetail)); }
        }

        public bool HasDetail => !string.IsNullOrWhiteSpace(_detail);

        public string Preview
        {
            get
            {
                if (!HasDetail) return "";
                return _detail.Trim().Split('\n')[0];
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>
    /// Plain-text "database" stored next to the exe (asistanim.txt).
    /// Format:
    ///   [NOTLAR]
    ///   - Baslik
    ///     | detay satiri
    ///   [FIKIRLER]
    ///   - ...
    /// </summary>
    public static class Storage
    {
        private const string NotesHeader = "[NOTLAR]";
        private const string IdeasHeader = "[FIKIRLER]";

        public static string FilePath
        {
            get
            {
                var exe = Environment.ProcessPath;
                var dir = string.IsNullOrEmpty(exe) ? AppContext.BaseDirectory : Path.GetDirectoryName(exe);
                return Path.Combine(dir, "asistanim.txt");
            }
        }

        public static string Serialize(IEnumerable<Entry> notes, IEnumerable<Entry> ideas)
        {
            var sb = new StringBuilder();
            Write(sb, NotesHeader, notes);
            Write(sb, IdeasHeader, ideas);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, string header, IEnumerable<Entry> items)
        {
            sb.Append(header).Append('\n');
            foreach (var e in items)
            {
                sb.Append("- ").Append(e.Title).Append('\n');
                if (e.Detail.Length > 0)
                    foreach (var line in e.Detail.Split('\n'))
                        sb.Append("  | ").Append(line).Append('\n');
            }
        }

        public static void Load(List<Entry> notes, List<Entry> ideas)
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                List<Entry> cur = null;
                Entry last = null;
                var detail = new List<string>();
                void Flush()
                {
                    if (last != null) last.Detail = string.Join("\n", detail);
                    detail.Clear();
                    last = null;
                }
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var line = raw.TrimEnd('\r');
                    if (line == NotesHeader) { Flush(); cur = notes; }
                    else if (line == IdeasHeader) { Flush(); cur = ideas; }
                    else if (cur != null && line.StartsWith("- "))
                    {
                        Flush();
                        last = new Entry { Title = line.Substring(2) };
                        cur.Add(last);
                    }
                    else if (last != null && line.StartsWith("  |"))
                    {
                        detail.Add(line.Length > 4 ? line.Substring(4) : "");
                    }
                }
                Flush();
            }
            catch { }
        }

        public static void Save(string text)
        {
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            File.Move(tmp, FilePath, true);
        }
    }
}
