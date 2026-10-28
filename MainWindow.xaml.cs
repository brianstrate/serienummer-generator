using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace seriel_number_generator
{
    public partial class MainWindow : Window
    {
        private readonly string _connectionString;

        public MainWindow()
        {
            InitializeComponent();

            // Gemmer databasen i ProgramData så programmet virker uden admin-rettigheder efter installation
            string commonFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string appFolder = Path.Combine(commonFolder, "SerienummerGenerator");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            string dbPath = Path.Combine(appFolder, "production.db");
            _connectionString = $"Data Source={dbPath};";

            InitDatabase();
            PopulateTableDropdown();
            PopulateMonthDropdown();
            RefreshAllDropdowns();
            SetDefaultYearMonth();
            LoadLastUsedData();
            LoadRecentSerials();
            LoadSerialsGrid();
            // Kører i baggrunden uden at blokere opstarten af brugerfladen
_ = UpdateChecker.CheckForUpdatesAsync(silentIfUpToDate: true);
        }

        private void PopulateMonthDropdown()
        {
            var months = new List<object>
            {
                new { Name = "01 - Januar", Value = "01" },
                new { Name = "02 - Februar", Value = "02" },
                new { Name = "03 - Marts", Value = "03" },
                new { Name = "04 - April", Value = "04" },
                new { Name = "05 - Maj", Value = "05" },
                new { Name = "06 - Juni", Value = "06" },
                new { Name = "07 - Juli", Value = "07" },
                new { Name = "08 - August", Value = "08" },
                new { Name = "09 - September", Value = "09" },
                new { Name = "10 - Oktober", Value = "10" },
                new { Name = "11 - November", Value = "11" },
                new { Name = "12 - December", Value = "12" }
            };

            CmbMonth.ItemsSource = months;
        }

        private void SetDefaultYearMonth()
        {
            TxtYear.Text = DateTime.Now.ToString("yyyy");
            CmbMonth.SelectedValue = DateTime.Now.ToString("MM");
        }

        private void InitDatabase()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmdPragma = conn.CreateCommand();
            cmdPragma.CommandText = "PRAGMA journal_mode = DELETE;";
            cmdPragma.ExecuteNonQuery();

            string[] tableDefs = new[]
            {
                "CREATE TABLE IF NOT EXISTS standard (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                "CREATE TABLE IF NOT EXISTS udi (id INTEGER PRIMARY KEY AUTOINCREMENT, udi TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                "CREATE TABLE IF NOT EXISTS ref (id INTEGER PRIMARY KEY AUTOINCREMENT, ref TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                "CREATE TABLE IF NOT EXISTS raps_batch (id INTEGER PRIMARY KEY AUTOINCREMENT, batch TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                "CREATE TABLE IF NOT EXISTS textile_batch (id INTEGER PRIMARY KEY AUTOINCREMENT, batch TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                "CREATE TABLE IF NOT EXISTS wash_temp (id INTEGER PRIMARY KEY AUTOINCREMENT, temp TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                "CREATE TABLE IF NOT EXISTS template (id INTEGER PRIMARY KEY AUTOINCREMENT, template_file TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                "CREATE TABLE IF NOT EXISTS settings (id INTEGER PRIMARY KEY AUTOINCREMENT, key TEXT NOT NULL UNIQUE, value TEXT NOT NULL, note TEXT, active INTEGER DEFAULT 1, created TEXT);",
                @"CREATE TABLE IF NOT EXISTS serial (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    seriel_num TEXT NOT NULL UNIQUE, 
                    standard_id INTEGER, 
                    udi_id INTEGER, 
                    ref_id INTEGER, 
                    rasp_id INTEGER, 
                    textile_id INTEGER, 
                    wash_temp_id INTEGER, 
                    template_id INTEGER, 
                    note TEXT, 
                    active INTEGER DEFAULT 1, 
                    created TEXT
                );"
            };

            foreach (var sql in tableDefs)
            {
                using var cmd = new SqliteCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            try
            {
                using var checkCmd = new SqliteCommand("INSERT OR IGNORE INTO settings (key, value, note, active, created) VALUES ('output_folder', 'output', 'Standard output mappe til vektorfiler', 1, datetime('now'));", conn);
                checkCmd.ExecuteNonQuery();
            }
            catch { }

            try
            {
                using var alterCmd = new SqliteCommand("ALTER TABLE serial ADD COLUMN wash_temp_id INTEGER;", conn);
                alterCmd.ExecuteNonQuery();
            }
            catch { }
        }

        private string GetOutputFolder()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                using var cmd = new SqliteCommand("SELECT value FROM settings WHERE key = 'output_folder' LIMIT 1;", conn);
                var res = cmd.ExecuteScalar();
                if (res != null && !string.IsNullOrWhiteSpace(res.ToString()))
                {
                    return res.ToString()!;
                }
            }
            catch { }

            return "output";
        }

        private void LoadLastUsedData()
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string sql = "SELECT standard_id, udi_id, ref_id, rasp_id, textile_id, wash_temp_id, template_id, note FROM serial ORDER BY id DESC LIMIT 1;";
                using var cmd = new SqliteCommand(sql, conn);
                using var rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    if (!rdr.IsDBNull(0)) CmbStandard.SelectedValue = rdr.GetInt32(0);
                    if (!rdr.IsDBNull(1)) CmbUdi.SelectedValue = rdr.GetInt32(1);
                    if (!rdr.IsDBNull(2)) CmbRef.SelectedValue = rdr.GetInt32(2);
                    if (!rdr.IsDBNull(3)) CmbRaps.SelectedValue = rdr.GetInt32(3);
                    if (!rdr.IsDBNull(4)) CmbTextile.SelectedValue = rdr.GetInt32(4);
                    if (!rdr.IsDBNull(5)) CmbWashTemp.SelectedValue = rdr.GetInt32(5);
                    if (!rdr.IsDBNull(6)) CmbTemplate.SelectedValue = rdr.GetInt32(6);
                    if (!rdr.IsDBNull(7)) TxtSerialNote.Text = rdr.GetString(7);
                }
            }
            catch { }
        }

        private string GenerateNextSerialNumber(SqliteConnection conn, string year, string month, SqliteTransaction? transaction = null)
        {
            string yearMonth = $"{year}{month}";
            string pattern = $"{yearMonth}%";

            var query = "SELECT seriel_num FROM serial WHERE seriel_num LIKE @pattern ORDER BY seriel_num DESC LIMIT 1;";
            using var cmd = new SqliteCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@pattern", pattern);
            
            var result = cmd.ExecuteScalar();
            int nextCounter = 100;

            if (result != null && result != DBNull.Value)
            {
                string lastSerial = result.ToString()!;
                if (lastSerial.Length > 6 && int.TryParse(lastSerial.Substring(6), out int lastCounter))
                {
                    nextCounter = lastCounter + 1;
                }
            }

            return $"{yearMonth}{nextCounter}";
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string year = TxtYear.Text.Trim();
            string month = CmbMonth.SelectedValue?.ToString() ?? "";

            if (year.Length != 4 || !int.TryParse(year, out _) || string.IsNullOrEmpty(month))
            {
                MessageBox.Show("Angiv et gyldigt 4-cifret årstal og vælg en måned.", "Ugyldig dato", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CmbStandard.SelectedValue == null || CmbUdi.SelectedValue == null || 
                CmbRef.SelectedValue == null || CmbRaps.SelectedValue == null || 
                CmbTextile.SelectedValue == null || CmbWashTemp.SelectedValue == null || 
                CmbTemplate.SelectedValue == null)
            {
                MessageBox.Show("Vælg venligst værdier i alle felter før generering.", "Mangler valg", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new InputDialog { Owner = this };
            if (dialog.ShowDialog() != true) return;

            int totalToCreate = dialog.NumberOfSerials;

            try
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                using var transaction = conn.BeginTransaction();

                var templateItem = (dynamic)CmbTemplate.SelectedItem;
                string templatePath = templateItem.TemplateFile;
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string lastCreatedSerial = "";

                string insertSql = @"INSERT INTO serial 
                    (seriel_num, standard_id, udi_id, ref_id, rasp_id, textile_id, wash_temp_id, template_id, note, active, created)
                    VALUES (@sn, @std, @udi, @ref, @raps, @txt, @wash, @tmpl, @note, 1, @now);";

                for (int i = 0; i < totalToCreate; i++)
                {
                    string serialNum = GenerateNextSerialNumber(conn, year, month, transaction);
                    lastCreatedSerial = serialNum;

                    using (var cmd = new SqliteCommand(insertSql, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@sn", serialNum);
                        cmd.Parameters.AddWithValue("@std", (int)CmbStandard.SelectedValue);
                        cmd.Parameters.AddWithValue("@udi", (int)CmbUdi.SelectedValue);
                        cmd.Parameters.AddWithValue("@ref", (int)CmbRef.SelectedValue);
                        cmd.Parameters.AddWithValue("@raps", (int)CmbRaps.SelectedValue);
                        cmd.Parameters.AddWithValue("@txt", (int)CmbTextile.SelectedValue);
                        cmd.Parameters.AddWithValue("@wash", (int)CmbWashTemp.SelectedValue);
                        cmd.Parameters.AddWithValue("@tmpl", (int)CmbTemplate.SelectedValue);
                        cmd.Parameters.AddWithValue("@note", TxtSerialNote.Text.Trim());
                        cmd.Parameters.AddWithValue("@now", now);
                        cmd.ExecuteNonQuery();
                    }

                    GenerateVectorOutput(templatePath, new Dictionary<string, string>
                    {
                        { "<<serienumber>>", serialNum },
                        { "<<standard>>", ((dynamic)CmbStandard.SelectedItem).Name },
                        { "<<udi>>", ((dynamic)CmbUdi.SelectedItem).Udi },
                        { "<<ref>>", ((dynamic)CmbRef.SelectedItem).Ref },
                        { "<<raps_batch>>", ((dynamic)CmbRaps.SelectedItem).Batch },
                        { "<<textile_batch>>", ((dynamic)CmbTextile.SelectedItem).Batch },
                        { "<<wash_temp>>", ((dynamic)CmbWashTemp.SelectedItem).Temp },
                        { "<<created>>", now },
                        { "<<note>>", TxtSerialNote.Text.Trim() }
                    }, serialNum);
                }

                transaction.Commit();

                LoadRecentSerials();
                LoadSerialsGrid();

                string outFolder = GetOutputFolder();
                string msg = totalToCreate == 1 
                    ? $"Serienummer {lastCreatedSerial} oprettet og vektorfil gemt!" 
                    : $"{totalToCreate} serienumre oprettet (til og med {lastCreatedSerial}) og vektorfiler gemt!";

                var resDialog = new ResultDialog(msg, outFolder) { Owner = this };
                resDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fejl: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateVectorOutput(string templateFilePath, Dictionary<string, string> tokens, string serialNum)
        {
            if (!File.Exists(templateFilePath))
            {
                MessageBox.Show($"Skabelonfilen '{templateFilePath}' blev ikke fundet. Vektorfil blev ikke genereret.", "Advarsel", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string content = File.ReadAllText(templateFilePath);

            foreach (var kvp in tokens)
            {
                content = content.Replace(kvp.Key, kvp.Value);

                string escapedKey = kvp.Key.Replace("<", "&lt;").Replace(">", "&gt;");
                content = content.Replace(escapedKey, kvp.Value);
            }

            string targetFolder = GetOutputFolder();
            Directory.CreateDirectory(targetFolder);

            string ext = Path.GetExtension(templateFilePath);
            if (string.IsNullOrEmpty(ext)) ext = ".svg";

            string outPath = Path.Combine(targetFolder, $"{serialNum}{ext}");
            File.WriteAllText(outPath, content);
        }

        // --- STAMDATA & HJÆLPEFUNKTIONER ---
        private void PopulateTableDropdown()
        {
            CmbTableSelect.ItemsSource = new List<string>
            {
                "standard", "udi", "ref", "raps_batch", "textile_batch", "wash_temp", "template", "settings"
            };
            CmbTableSelect.SelectedIndex = 0;
        }

        private string GetValueColumnName(string table) => table switch
        {
            "standard" => "name",
            "udi" => "udi",
            "ref" => "ref",
            "raps_batch" => "batch",
            "textile_batch" => "batch",
            "wash_temp" => "temp",
            "template" => "template_file",
            "settings" => "value",
            _ => "name"
        };

        private void RefreshAllDropdowns()
        {
            var stdList = FetchLookupData("standard", "name", id => new { Id = id.id, Name = id.val });
            CmbStandard.ItemsSource = stdList;
            CmbEditStandard.ItemsSource = stdList;

            var udiList = FetchLookupData("udi", "udi", id => new { Id = id.id, Udi = id.val });
            CmbUdi.ItemsSource = udiList;
            CmbEditUdi.ItemsSource = udiList;

            var refList = FetchLookupData("ref", "ref", id => new { Id = id.id, Ref = id.val });
            CmbRef.ItemsSource = refList;
            CmbEditRef.ItemsSource = refList;

            var rapsList = FetchLookupData("raps_batch", "batch", id => new { Id = id.id, Batch = id.val });
            CmbRaps.ItemsSource = rapsList;
            CmbEditRaps.ItemsSource = rapsList;

            var txtList = FetchLookupData("textile_batch", "batch", id => new { Id = id.id, Batch = id.val });
            CmbTextile.ItemsSource = txtList;
            CmbEditTextile.ItemsSource = txtList;

            var washList = FetchLookupData("wash_temp", "temp", id => new { Id = id.id, Temp = id.val });
            CmbWashTemp.ItemsSource = washList;
            CmbEditWashTemp.ItemsSource = washList;

            var tmplList = FetchLookupData("template", "template_file", id => new { Id = id.id, TemplateFile = id.val });
            CmbTemplate.ItemsSource = tmplList;
            CmbEditTemplate.ItemsSource = tmplList;
        }

        private List<T> FetchLookupData<T>(string tableName, string colName, Func<(int id, string val), T> mapper)
        {
            var list = new List<T>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string query = $"SELECT id, {colName} FROM {tableName} WHERE active = 1 ORDER BY id DESC;";
            using var cmd = new SqliteCommand(query, conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(mapper((rdr.GetInt32(0), rdr.GetString(1))));
            }
            return list;
        }

        private void CmbTableSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ResetStamdataForm();
            LoadGridData();

            string selected = CmbTableSelect.SelectedItem?.ToString() ?? "";
            BtnBrowseTemplate.Visibility = selected == "template" ? Visibility.Visible : Visibility.Collapsed;
            BtnBrowseFolder.Visibility = selected == "settings" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnBrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Vælg Vektor Skabelonfil",
                Filter = "Vektorfiler (*.svg;*.ai;*.eps)|*.svg;*.ai;*.eps|SVG Filer (*.svg)|*.svg|Alle filer (*.*)|*.*",
                InitialDirectory = Environment.CurrentDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                TxtNewValue.Text = dialog.FileName;
            }
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Vælg output-mappe til vektorfiler",
                InitialDirectory = Environment.CurrentDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                TxtNewValue.Text = dialog.FolderName;
            }
        }

        private void LoadGridData()
        {
            if (CmbTableSelect.SelectedItem == null) return;
            string table = CmbTableSelect.SelectedItem.ToString()!;

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = new SqliteCommand($"SELECT * FROM {table} ORDER BY id DESC;", conn);
            using var rdr = cmd.ExecuteReader();
            var dt = new DataTable();
            dt.Load(rdr);
            GridRecords.ItemsSource = dt.DefaultView;
        }

        private void GridRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridRecords.SelectedItem is DataRowView row)
            {
                string table = CmbTableSelect.SelectedItem.ToString()!;
                string valCol = GetValueColumnName(table);

                TxtRecordId.Text = row["id"].ToString();
                TxtNewValue.Text = row[valCol].ToString();
                TxtNewNote.Text = row["note"]?.ToString() ?? "";
                ChkRecordActive.IsChecked = Convert.ToInt32(row["active"]) == 1;
                BtnSaveRecord.Content = "Gem ændring";
            }
        }

        private void BtnSaveRecord_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewValue.Text))
            {
                MessageBox.Show("Angiv venligst en værdi.", "Mangler input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string table = CmbTableSelect.SelectedItem.ToString()!;
            string valCol = GetValueColumnName(table);
            int active = ChkRecordActive.IsChecked == true ? 1 : 0;

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            if (string.IsNullOrEmpty(TxtRecordId.Text))
            {
                string sql = table == "settings" 
                    ? "INSERT INTO settings (key, value, note, active, created) VALUES (@note, @val, @note, @active, @now);"
                    : $"INSERT INTO {table} ({valCol}, note, active, created) VALUES (@val, @note, @active, @now);";

                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@val", TxtNewValue.Text.Trim());
                cmd.Parameters.AddWithValue("@note", TxtNewNote.Text.Trim());
                cmd.Parameters.AddWithValue("@active", active);
                cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
            else
            {
                string sql = $"UPDATE {table} SET {valCol} = @val, note = @note, active = @active WHERE id = @id;";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@val", TxtNewValue.Text.Trim());
                cmd.Parameters.AddWithValue("@note", TxtNewNote.Text.Trim());
                cmd.Parameters.AddWithValue("@active", active);
                cmd.Parameters.AddWithValue("@id", int.Parse(TxtRecordId.Text));
                cmd.ExecuteNonQuery();
            }

            ResetStamdataForm();
            LoadGridData();
            RefreshAllDropdowns();
        }

        private void BtnCancelEdit_Click(object sender, RoutedEventArgs e) => ResetStamdataForm();

        private void ResetStamdataForm()
        {
            TxtRecordId.Clear();
            TxtNewValue.Clear();
            TxtNewNote.Clear();
            ChkRecordActive.IsChecked = true;
            BtnSaveRecord.Content = "Opret ny";
            if (GridRecords != null) GridRecords.SelectedItem = null;
        }

        // --- SERIENUMMER ADMINISTRATION ---
        private void LoadSerialsGrid()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = @"
                SELECT s.id, s.seriel_num, std.name as standard, u.udi, r.ref, 
                       rb.batch as raps_batch, tb.batch as textile_batch, wt.temp as wash_temp,
                       tmpl.template_file, s.note, s.active, s.created,
                       s.standard_id, s.udi_id, s.ref_id, s.rasp_id, s.textile_id, s.wash_temp_id, s.template_id
                FROM serial s
                LEFT JOIN standard std ON s.standard_id = std.id
                LEFT JOIN udi u ON s.udi_id = u.id
                LEFT JOIN ref r ON s.ref_id = r.id
                LEFT JOIN raps_batch rb ON s.rasp_id = rb.id
                LEFT JOIN textile_batch tb ON s.textile_id = tb.id
                LEFT JOIN wash_temp wt ON s.wash_temp_id = wt.id
                LEFT JOIN template tmpl ON s.template_id = tmpl.id
                ORDER BY s.id DESC;";

            using var cmd = new SqliteCommand(sql, conn);
            using var rdr = cmd.ExecuteReader();
            var dt = new DataTable();
            dt.Load(rdr);
            GridSerials.ItemsSource = dt.DefaultView;
        }

        private void GridSerials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridSerials.SelectedItem is DataRowView row)
            {
                TxtEditSerialNum.Text = row["seriel_num"].ToString();
                TxtEditSerialNote.Text = row["note"]?.ToString() ?? "";
                ChkEditSerialActive.IsChecked = Convert.ToInt32(row["active"]) == 1;

                if (int.TryParse(row["standard_id"]?.ToString(), out int stdId)) CmbEditStandard.SelectedValue = stdId;
                if (int.TryParse(row["udi_id"]?.ToString(), out int udiId)) CmbEditUdi.SelectedValue = udiId;
                if (int.TryParse(row["ref_id"]?.ToString(), out int refId)) CmbEditRef.SelectedValue = refId;
                if (int.TryParse(row["rasp_id"]?.ToString(), out int rapsId)) CmbEditRaps.SelectedValue = rapsId;
                if (int.TryParse(row["textile_id"]?.ToString(), out int txtId)) CmbEditTextile.SelectedValue = txtId;
                if (int.TryParse(row["wash_temp_id"]?.ToString(), out int washId)) CmbEditWashTemp.SelectedValue = washId;
                if (int.TryParse(row["template_id"]?.ToString(), out int tmplId)) CmbEditTemplate.SelectedValue = tmplId;
            }
        }

        private void BtnUpdateSerial_Click(object sender, RoutedEventArgs e)
        {
            if (GridSerials.SelectedItem is not DataRowView row)
            {
                MessageBox.Show("Vælg et serienummer i tabellen ovenfor først.", "Ingen markering", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int id = Convert.ToInt32(row["id"]);
            int active = ChkEditSerialActive.IsChecked == true ? 1 : 0;

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = @"
                UPDATE serial 
                SET standard_id = @std, 
                    udi_id = @udi, 
                    ref_id = @ref, 
                    rasp_id = @raps, 
                    textile_id = @txt, 
                    wash_temp_id = @wash,
                    template_id = @tmpl, 
                    note = @note, 
                    active = @active
                WHERE id = @id;";

            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@std", (int)(CmbEditStandard.SelectedValue ?? 0));
            cmd.Parameters.AddWithValue("@udi", (int)(CmbEditUdi.SelectedValue ?? 0));
            cmd.Parameters.AddWithValue("@ref", (int)(CmbEditRef.SelectedValue ?? 0));
            cmd.Parameters.AddWithValue("@raps", (int)(CmbEditRaps.SelectedValue ?? 0));
            cmd.Parameters.AddWithValue("@txt", (int)(CmbEditTextile.SelectedValue ?? 0));
            cmd.Parameters.AddWithValue("@wash", (int)(CmbEditWashTemp.SelectedValue ?? 0));
            cmd.Parameters.AddWithValue("@tmpl", (int)(CmbEditTemplate.SelectedValue ?? 0));
            cmd.Parameters.AddWithValue("@note", TxtEditSerialNote.Text.Trim());
            cmd.Parameters.AddWithValue("@active", active);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            LoadSerialsGrid();
            LoadRecentSerials();
            MessageBox.Show($"Serienummer {TxtEditSerialNum.Text} er opdateret!", "Gemt", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRegenerateVector_Click(object sender, RoutedEventArgs e)
        {
            if (CmbEditTemplate.SelectedItem == null)
            {
                MessageBox.Show("Vælg en gyldig skabelon før regenerering.", "Mangler skabelon", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string serialNum = TxtEditSerialNum.Text;
            var templateItem = (dynamic)CmbEditTemplate.SelectedItem;
            string templatePath = templateItem.TemplateFile;

            GenerateVectorOutput(templatePath, new Dictionary<string, string>
            {
                { "<<serienumber>>", serialNum },
                { "<<standard>>", CmbEditStandard.SelectedItem != null ? ((dynamic)CmbEditStandard.SelectedItem).Name : "" },
                { "<<udi>>", CmbEditUdi.SelectedItem != null ? ((dynamic)CmbEditUdi.SelectedItem).Udi : "" },
                { "<<ref>>", CmbEditRef.SelectedItem != null ? ((dynamic)CmbEditRef.SelectedItem).Ref : "" },
                { "<<raps_batch>>", CmbEditRaps.SelectedItem != null ? ((dynamic)CmbEditRaps.SelectedItem).Batch : "" },
                { "<<textile_batch>>", CmbEditTextile.SelectedItem != null ? ((dynamic)CmbEditTextile.SelectedItem).Batch : "" },
                { "<<wash_temp>>", CmbEditWashTemp.SelectedItem != null ? ((dynamic)CmbEditWashTemp.SelectedItem).Temp : "" },
                { "<<created>>", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "<<note>>", TxtEditSerialNote.Text.Trim() }
            }, serialNum);

            string outFolder = GetOutputFolder();
            var resDialog = new ResultDialog($"Vektorfil for {serialNum} blev genskabt!", outFolder) { Owner = this };
            resDialog.ShowDialog();
        }

        private void LoadRecentSerials()
        {
            LstSerials.Items.Clear();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            string sql = "SELECT seriel_num, created, active FROM serial ORDER BY id DESC LIMIT 25;";
            using var cmd = new SqliteCommand(sql, conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string status = rdr.GetInt32(2) == 1 ? "" : " [DEAKTIVERET]";
                LstSerials.Items.Add($"{rdr.GetString(0)}  ({rdr.GetString(1)}){status}");
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

        private void MenuTemplateHelp_Click(object sender, RoutedEventArgs e)
        {
            var helpWin = new TemplateHelpWindow { Owner = this };
            helpWin.ShowDialog();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e) => 
            MessageBox.Show("Serienummer- og Vektorgenerator v1.4", "Om", MessageBoxButton.OK, MessageBoxImage.Information);

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Gem Serienumre som CSV",
                Filter = "CSV-fil (*.csv)|*.csv|Alle filer (*.*)|*.*",
                FileName = $"serienumre_eksport_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                DefaultExt = ".csv"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                var csvBuilder = new StringBuilder();
                csvBuilder.AppendLine("ID;Serienummer;Standard;UDI;REF;Raps Batch;Tekstil Batch;Vasketemperatur;Skabelon;Note;Aktiv;Oprettet");

                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                string query = @"
                    SELECT s.id, 
                           s.seriel_num, 
                           COALESCE(std.name, '') as standard, 
                           COALESCE(u.udi, '') as udi, 
                           COALESCE(r.ref, '') as ref, 
                           COALESCE(rb.batch, '') as raps_batch, 
                           COALESCE(tb.batch, '') as textile_batch, 
                           COALESCE(wt.temp, '') as wash_temp, 
                           COALESCE(tmpl.template_file, '') as template_file, 
                           COALESCE(s.note, '') as note, 
                           s.active, 
                           s.created
                    FROM serial s
                    LEFT JOIN standard std ON s.standard_id = std.id
                    LEFT JOIN udi u ON s.udi_id = u.id
                    LEFT JOIN ref r ON s.ref_id = r.id
                    LEFT JOIN raps_batch rb ON s.rasp_id = rb.id
                    LEFT JOIN textile_batch tb ON s.textile_id = tb.id
                    LEFT JOIN wash_temp wt ON s.wash_temp_id = wt.id
                    LEFT JOIN template tmpl ON s.template_id = tmpl.id
                    ORDER BY s.id ASC;";

                using var cmd = new SqliteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                int exportCount = 0;
                while (reader.Read())
                {
                    string id = reader.GetInt32(0).ToString();
                    string serial = EscapeCsvField(reader.GetString(1));
                    string standard = EscapeCsvField(reader.GetString(2));
                    string udi = EscapeCsvField(reader.GetString(3));
                    string refVal = EscapeCsvField(reader.GetString(4));
                    string raps = EscapeCsvField(reader.GetString(5));
                    string textile = EscapeCsvField(reader.GetString(6));
                    string washTemp = EscapeCsvField(reader.GetString(7));
                    string template = EscapeCsvField(reader.GetString(8));
                    string note = EscapeCsvField(reader.GetString(9));
                    string active = reader.GetInt32(10) == 1 ? "Ja" : "Nej";
                    string created = EscapeCsvField(reader.GetString(11));

                    csvBuilder.AppendLine($"{id};{serial};{standard};{udi};{refVal};{raps};{textile};{washTemp};{template};{note};{active};{created}");
                    exportCount++;
                }

                File.WriteAllText(saveDialog.FileName, csvBuilder.ToString(), Encoding.UTF8);

                MessageBox.Show($"Eksport gennemført!\n{exportCount} rækker gemt i:\n{saveDialog.FileName}", 
                                "Eksport Gennemført", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Der opstod en fejl under eksporten: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(";") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }  

        private async void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
{
    await UpdateChecker.CheckForUpdatesAsync(silentIfUpToDate: false);
}  
    }
}