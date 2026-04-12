using SmartExpenseAnalyzer.Models;
using SmartExpenseAnalyzer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace SmartExpenseAnalyzer.UI
{

    public partial class MainForm : Form
    {
        // ── Theme colors ──────────────────────────────────────────────────
        private static readonly Color TitleBarColor = Color.FromArgb(70, 130, 180);
        private static readonly Color BtnBlue = Color.FromArgb(70, 130, 180);
        private static readonly Color BtnGreen = Color.FromArgb(39, 174, 96);
        private static readonly Color BtnRed = Color.FromArgb(192, 57, 43);
        private static readonly Color StatBlue = Color.FromArgb(52, 109, 172);
        private static readonly Color StatOrange = Color.FromArgb(211, 84, 0);
        private static readonly Color StatGreen = Color.FromArgb(39, 174, 96);

        // ── In-memory data store (starts empty) ──────────────────────────
        private readonly List<(string Date, string Category, double Amount, string Desc)> _expenses
            = new List<(string, string, double, string)>();

        // ── Controls referenced after construction ────────────────────────
        private DataGridView dgvHistory;
        private Label lblTotal;
        private ListBox lstAlerts;
        private TextBox txtSearch;
        private ComboBox cmbFilterCat;
        private ComboBox cmbFilterMonth;

        // Charts (updated when expenses change)
        private Chart _pie;
        private Chart _lineChart;

        // Input controls (Add New Expense panel)
        private TextBox txtAmount;
        private ComboBox cmbCategory;
        private DateTimePicker dtpDate;
        private TextBox txtDescription;

        // Spending Insights panel controls
        private Label _lblTopCategory;
        private Label _lblExpensiveDay;
        private Label _lblAvgDaily;
        private ListBox _lstSuggestions;

        private readonly ExpenseManager _expenseManager = new ExpenseManager();

        // ── Constructor ───────────────────────────────────────────────────
        public MainForm()
        {
            this.Text = "Smart Expense Analyzer";
            this.WindowState = FormWindowState.Maximized;   // fills the desktop
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(215, 225, 240);
            this.Font = new Font("Segoe UI", 9f);
            this.MinimumSize = new Size(1024, 700);

            BuildUI();
            LoadExpensesFromStorage();

            // Wire Resize so panels re-flow when user un-maximises
            this.Resize += (s, e) => RepositionPanels();
        }

        // ══════════════════════════════════════════════════════════════════
        //  PANEL REFERENCES for dynamic re-layout
        // ══════════════════════════════════════════════════════════════════
        private Panel _panelMain;
        private Panel _panelAdd;
        private Panel _panelHistory;
        private Panel _panelInsights;

        private void BuildUI()
        {
            _panelMain = BuildMainAnalyzerPanel();
            _panelAdd = BuildAddExpensePanel();
            _panelHistory = BuildExpenseHistoryPanel();
            _panelInsights = BuildSpendingInsightsPanel();

            this.Controls.AddRange(new Control[]
            {
                _panelMain, _panelHistory, _panelAdd, _panelInsights
            });

            RepositionPanels();
        }

        /// <summary>Recalculates panel positions/sizes relative to the current client area.</summary>
        private void RepositionPanels()
        {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            int margin = 12;
            int colW = (w - margin * 3) / 2;   // two equal columns
            int col2X = margin + colW + margin;

            // Row split: top ~58%, bottom ~42%
            int topH = (int)((h - margin * 3) * 0.58);
            int botH = h - margin * 3 - topH;
            int botY = margin + topH + margin;

            _panelMain.SetBounds(margin, margin, colW, topH);
            _panelHistory.SetBounds(margin, botY, colW, botH);
            _panelAdd.SetBounds(col2X, margin, colW, topH);
            _panelInsights.SetBounds(col2X, botY, colW, botH);
        }

        // ══════════════════════════════════════════════════════════════════
        //  PANEL 1 — SMART EXPENSE ANALYZER (top-left)
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildMainAnalyzerPanel()
        {
            var panel = CreateMiniWindow("Smart Expense Analyzer");

            // ── Stat boxes ───────────────────────────────────────────────
            var statsFlow = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(10, 44),
                Size = new Size(638, 68),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0)
            };
            statsFlow.Controls.Add(MakeStatBox("Total Balance:", "₹ 0", StatBlue));
            statsFlow.Controls.Add(MakeStatBox("Total Spent:", "₹ 0", StatOrange));
            statsFlow.Controls.Add(MakeStatBox("Savings:", "₹ 0", StatGreen));
            panel.Controls.Add(statsFlow);

            // ── Charts ───────────────────────────────────────────────────
            var chartPanel = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(10, 120),
                Size = new Size(638, 280),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            chartPanel.Controls.Add(new Label
            {
                Text = "Spending by Category",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TitleBarColor,
                Location = new Point(8, 6),
                AutoSize = true
            });

            // Pie chart
            _pie = new Chart { Location = new Point(0, 30), Size = new Size(300, 240), BackColor = Color.White };
            var pieArea = new ChartArea { BackColor = Color.White };
            _pie.ChartAreas.Add(pieArea);
            _pie.Legends.Add(new Legend { Docking = Docking.Right, BackColor = Color.White });
            chartPanel.Controls.Add(_pie);

            // Line chart
            _lineChart = new Chart { Location = new Point(305, 30), Size = new Size(330, 240), BackColor = Color.White };
            var lineArea = new ChartArea { BackColor = Color.White };
            lineArea.AxisX.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            lineArea.AxisY.MajorGrid.LineColor = Color.FromArgb(220, 220, 220);
            _lineChart.ChartAreas.Add(lineArea);
            chartPanel.Controls.Add(_lineChart);

            panel.Controls.Add(chartPanel);

            // ── Alerts & Insights ────────────────────────────────────────
            var alertsOuter = new Panel
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(10, 410),
                Size = new Size(638, 90),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            var alertHeader = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = TitleBarColor };
            alertHeader.Controls.Add(new Label
            {
                Text = "Alerts & Insights:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(8, 0),
                Size = new Size(300, 28),
                TextAlign = ContentAlignment.MiddleLeft
            });
            alertsOuter.Controls.Add(alertHeader);

            lstAlerts = new ListBox
            {
                Location = new Point(0, 28),
                Size = new Size(636, 60),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White
            };
            lstAlerts.Items.Add("ℹ  No expenses recorded yet. Add your first expense!");
            alertsOuter.Controls.Add(lstAlerts);
            panel.Controls.Add(alertsOuter);

            // ── Bottom buttons ───────────────────────────────────────────
            panel.Controls.Add(MakeButton("Add Expense", BtnBlue, 10, 512));
            panel.Controls.Add(MakeButton("View History", BtnBlue, 230, 512));
            panel.Controls.Add(MakeButton("Reports", BtnRed, 450, 512));

            return panel;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PANEL 2 — ADD NEW EXPENSE (top-right)
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildAddExpensePanel()
        {
            var panel = CreateMiniWindow("Add New Expense");

            var tbl = new TableLayoutPanel
            {
                Location = new Point(20, 50),
                Size = new Size(618, 208),
                ColumnCount = 2,
                RowCount = 4,
                BackColor = Color.White
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            // Amount
            tbl.Controls.Add(MakeFormLabel("Amount:"), 0, 0);
            txtAmount = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4), Font = new Font("Segoe UI", 10f) };
            tbl.Controls.Add(txtAmount, 1, 0);

            // Category
            tbl.Controls.Add(MakeFormLabel("Category:"), 0, 1);
            cmbCategory = new ComboBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10f)
            };
            cmbCategory.Items.AddRange(new[] { "Food", "Travel", "Shopping", "Bills" });
            cmbCategory.SelectedIndex = 0;
            tbl.Controls.Add(cmbCategory, 1, 1);

            // Date
            tbl.Controls.Add(MakeFormLabel("Date:"), 0, 2);
            dtpDate = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Format = DateTimePickerFormat.Short,
                Font = new Font("Segoe UI", 10f),
                Value = DateTime.Today
            };
            tbl.Controls.Add(dtpDate, 1, 2);

            // Description
            tbl.Controls.Add(MakeFormLabel("Description:"), 0, 3);
            txtDescription = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Font = new Font("Segoe UI", 10f)
            };
            tbl.Controls.Add(txtDescription, 1, 3);

            panel.Controls.Add(tbl);

            var btnSave = MakeButton("Save", BtnBlue, 370, 282);
            var btnCancel = MakeButton("Cancel", Color.FromArgb(140, 140, 140), 520, 282);
            btnSave.Size = new Size(130, 40);
            btnCancel.Size = new Size(110, 40);
            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) =>
            {
                txtAmount.Clear();
                txtDescription.Clear();
                cmbCategory.SelectedIndex = 0;
                dtpDate.Value = DateTime.Today;
            };

            panel.Controls.Add(btnSave);
            panel.Controls.Add(btnCancel);

            return panel;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PANEL 3 — EXPENSE HISTORY (bottom-left)
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildExpenseHistoryPanel()
        {
            var panel = CreateMiniWindow("Expense History");

            var filterRow = new FlowLayoutPanel
            {
                Location = new Point(10, 42),
                Size = new Size(638, 34),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            filterRow.Controls.Add(new Label
            {
                Text = "Filter:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(0, 6, 0, 0)
            });

            cmbFilterCat = new ComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(4, 2, 4, 0)
            };
            cmbFilterCat.Items.AddRange(new[] { "All Categories", "Food", "Travel", "Shopping", "Bills" });
            cmbFilterCat.SelectedIndex = 0;
            cmbFilterCat.SelectedIndexChanged += (s, e) => ApplyFilters();

            cmbFilterMonth = new ComboBox
            {
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(4, 2, 4, 0)
            };
            cmbFilterMonth.Items.AddRange(new[]
            {
                "All", "January", "February", "March", "April",
                "May", "June", "July", "August", "September",
                "October", "November", "December"
            });
            cmbFilterMonth.SelectedIndex = 0;
            cmbFilterMonth.SelectedIndexChanged += (s, e) => ApplyFilters();

            txtSearch = new TextBox
            {
                Width = 160,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(4, 2, 4, 0)
            };
            txtSearch.TextChanged += (s, e) => ApplyFilters();

            filterRow.Controls.AddRange(new Control[] { cmbFilterCat, cmbFilterMonth, txtSearch });
            panel.Controls.Add(filterRow);

            dgvHistory = new DataGridView
            {
                Location = new Point(10, 82),
                Size = new Size(638, 150),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9f),
                GridColor = Color.FromArgb(220, 225, 232)
            };
            dgvHistory.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            dgvHistory.ColumnHeadersDefaultCellStyle.ForeColor = TitleBarColor;
            dgvHistory.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgvHistory.EnableHeadersVisualStyles = false;
            dgvHistory.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 252);

            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 18 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 18 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount", FillWeight = 14 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Desc", HeaderText = "Description", FillWeight = 50 });
            panel.Controls.Add(dgvHistory);

            lblTotal = new Label
            {
                Text = "Total:   ₹0",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point(420, 240),
                Size = new Size(220, 26),
                TextAlign = ContentAlignment.MiddleRight
            };
            panel.Controls.Add(lblTotal);

            LoadGrid();
            return panel;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PANEL 4 — SPENDING INSIGHTS (bottom-right)
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildSpendingInsightsPanel()
        {
            var panel = CreateMiniWindow("Spending Insights");

            // ── Stats Box ────────────────────────────────────────────────────
            var statsBox = new Panel
            {
                Location = new Point(10, 45),
                Size = new Size(638, 120),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Build rows and capture the value labels
            _lblTopCategory = AddInsightRow(statsBox, "Top Spending Category:", "—", 12);
            _lblExpensiveDay = AddInsightRow(statsBox, "Most Expensive Day:", "—", 48);
            _lblAvgDaily = AddInsightRow(statsBox, "Average Daily Spend:", "₹ 0", 84);

            panel.Controls.Add(statsBox);

            // ── Suggestions Box ───────────────────────────────────────────────
            var sugBox = new Panel
            {
                Location = new Point(10, 178),
                Size = new Size(638, 160),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var sugHeader = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = TitleBarColor };
            sugHeader.Controls.Add(new Label
            {
                Text = "Suggestions:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(8, 0),
                Size = new Size(300, 30),
                TextAlign = ContentAlignment.MiddleLeft
            });
            sugBox.Controls.Add(sugHeader);

            _lstSuggestions = new ListBox
            {
                Location = new Point(0, 30),
                Size = new Size(636, 128),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White
            };
            _lstSuggestions.Items.Add("  •  Add expenses to see personalised suggestions.");
            sugBox.Controls.Add(_lstSuggestions);
            panel.Controls.Add(sugBox);

            // ── Generate Report Button ────────────────────────────────────────
            var btnReport = MakeButton("Generate Report PDF", BtnBlue, 190, 360);
            btnReport.Size = new Size(260, 42);
            btnReport.Click += BtnGenerateReport_Click;
            panel.Controls.Add(btnReport);

            return panel;
        }

        // ── Row factory that returns the value label so we can update it later ──
        private Label AddInsightRow(Panel parent, string key, string value, int y)
        {
            var row = new Panel { Location = new Point(0, y), Size = new Size(638, 36) };

            row.Controls.Add(new Label
            {
                Text = key,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TitleBarColor,
                Location = new Point(12, 0),
                Size = new Size(240, 36),
                TextAlign = ContentAlignment.MiddleLeft
            });

            var valLabel = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point(252, 0),
                Size = new Size(370, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };

            row.Controls.Add(valLabel);
            parent.Controls.Add(row);
            return valLabel;   // caller stores this reference
        }

        // ══════════════════════════════════════════════════════════════════
        //  MINI-WINDOW FACTORY
        // ══════════════════════════════════════════════════════════════════
        private Panel CreateMiniWindow(string title)
        {
            var outer = new Panel { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            var titleBar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = TitleBarColor };
            var lblTitle = new Label
            {
                Text = title,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(10, 0),
                Size = new Size(500, 34),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnClose = MakeTitleBarButton("✕");
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 28);
            btnClose.Click += (s, e) => outer.Visible = false;

            var btnMax = MakeTitleBarButton("□");

            var btnMin = MakeTitleBarButton("−");
            bool minimized = false; int savedHeight = 300;
            btnMin.Click += (s, e) =>
            {
                minimized = !minimized;
                if (minimized) { savedHeight = outer.Height; outer.Height = 36; btnMin.Text = "↑"; }
                else { outer.Height = savedHeight; btnMin.Text = "−"; }
            };

            outer.Resize += (s, e) =>
            {
                btnClose.Location = new Point(outer.Width - 36, 3);
                btnMax.Location = new Point(outer.Width - 68, 3);
                btnMin.Location = new Point(outer.Width - 100, 3);
            };

            Point dragOrigin = Point.Empty; bool dragging = false;
            MouseEventHandler mDown = (s, e) => { dragging = true; dragOrigin = e.Location; };
            MouseEventHandler mUp = (s, e) => { dragging = false; };
            MouseEventHandler mMove = (s, e) =>
            {
                if (!dragging) return;
                outer.Left += e.X - dragOrigin.X;
                outer.Top += e.Y - dragOrigin.Y;
            };
            titleBar.MouseDown += mDown; titleBar.MouseUp += mUp; titleBar.MouseMove += mMove;
            lblTitle.MouseDown += mDown; lblTitle.MouseUp += mUp; lblTitle.MouseMove += mMove;

            titleBar.Controls.AddRange(new Control[] { lblTitle, btnMin, btnMax, btnClose });
            outer.Controls.Add(titleBar);
            return outer;
        }

        private Button MakeTitleBarButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(28, 28),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", text == "−" ? 12f : 9f),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 255, 255, 255);
            return b;
        }

        // ══════════════════════════════════════════════════════════════════
        //  BUSINESS LOGIC
        // ══════════════════════════════════════════════════════════════════
        private void BtnSave_Click(object sender, EventArgs e)
        {
            // ── Validation ────────────────────────────────────────────────────
            if (!double.TryParse(txtAmount.Text.Trim(), out double amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Invalid Input",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ── Build Expense object ──────────────────────────────────────────
            var expense = new Expense
            {
                Amount = amount,
                Category = cmbCategory.SelectedItem.ToString(),
                Date = dtpDate.Value,
                Note = txtDescription.Text.Trim()
            };

            // ── Save via ExpenseManager (MongoDB or JSON fallback) ────────────
            try
            {
                _expenseManager.Add(expense);

                _expenses.Add((
                    expense.Date.ToString("dd/MM/yyyy"),
                    expense.Category,
                    expense.Amount,
                    expense.Note
                ));

                // ── Refresh UI ────────────────────────────────────────
                UpdateStatBoxes();
                UpdateAlerts();
                UpdateCharts();
                UpdateInsightsPanel();
                LoadGrid();
                dgvHistory.DataSource = null;
                dgvHistory.DataSource = _expenses
                    .Select(x => new { x.Date, x.Category, x.Amount, x.Desc })
                    .ToList();

                txtAmount.Clear();
                txtDescription.Clear();
                cmbCategory.SelectedIndex = 0;
                dtpDate.Value = DateTime.Today;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save expense:\n{ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadGrid()
        {
            if (dgvHistory == null) return;

            // 1. Get filter values
            string catFilter = cmbFilterCat?.SelectedItem?.ToString() ?? "All Categories";
            string monthFilter = cmbFilterMonth?.SelectedItem?.ToString() ?? "All";
            string search = txtSearch?.Text?.Trim() ?? "";

            // 2. Filter the in-memory list using LINQ
            var filteredList = _expenses.Where(exp =>
            {
                bool passCat = catFilter == "All Categories" || exp.Category == catFilter;

                bool passMonth = monthFilter == "All";
                if (!passMonth)
                {
                    string[] parts = exp.Date.Split('/');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int mNum))
                    {
                        string monthName = System.Globalization.CultureInfo.CurrentCulture
                                           .DateTimeFormat.GetMonthName(mNum);
                        passMonth = string.Equals(monthName, monthFilter, StringComparison.OrdinalIgnoreCase);
                    }
                }

                bool passSearch = string.IsNullOrEmpty(search)
                    || exp.Desc.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || exp.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

                return passCat && passMonth && passSearch;
            }).Select(x => new { x.Date, x.Category, x.Amount, x.Desc }).ToList();

            // 3. Update the Grid via DataSource (Fixes the InvalidOperationException)
            dgvHistory.DataSource = null;
            dgvHistory.DataSource = filteredList;

            // 4. Update the Total Label
            double total = filteredList.Sum(x => x.Amount);
            if (lblTotal != null)
                lblTotal.Text = $"Total:   ₹{total:N0}";
        }

        private void ApplyFilters() => LoadGrid();

        private void UpdateAlerts()
        {
            if (lstAlerts == null) return;
            lstAlerts.Items.Clear();

            if (_expenses.Count == 0)
            {
                lstAlerts.Items.Add("ℹ  No expenses recorded yet. Add your first expense!");
                return;
            }

            double total = 0;
            var cat = new Dictionary<string, double>();
            foreach (var exp in _expenses)
            {
                total += exp.Amount;
                if (!cat.ContainsKey(exp.Category)) cat[exp.Category] = 0;
                cat[exp.Category] += exp.Amount;
            }

            if (cat.TryGetValue("Food", out double food) && food > 5000)
                lstAlerts.Items.Add("⚠  You overspent on Food this month!");
            if (cat.TryGetValue("Shopping", out double shop) && shop > 3000)
                lstAlerts.Items.Add("⚠  Shopping budget exceeded!");

            foreach (var kv in cat)
            {
                double pct = total > 0 ? (kv.Value / total) * 100 : 0;
                if (pct > 40)
                    lstAlerts.Items.Add($"💡  {kv.Key} is {pct:F0}% of spending — consider reducing.");
            }

            if (lstAlerts.Items.Count == 0)
                lstAlerts.Items.Add("✅  All spending within budget. Great job!");
        }

        private void UpdateCharts()
        {
            if (_pie == null || _lineChart == null) return;

            // ── Pie: spending by category ─────────────────────────────────
            _pie.Series.Clear();
            var pieSeries = new Series("Cat")
            {
                ChartType = SeriesChartType.Pie,
                IsValueShownAsLabel = false
            };
            pieSeries["PieStartAngle"] = "270";

            var catTotals = new Dictionary<string, double>();
            foreach (var exp in _expenses)
            {
                if (!catTotals.ContainsKey(exp.Category)) catTotals[exp.Category] = 0;
                catTotals[exp.Category] += exp.Amount;
            }

            Color[] pieColors = {
                Color.FromArgb(231, 76,  60),
                Color.FromArgb(41,  128, 185),
                Color.FromArgb(230, 126, 34),
                Color.FromArgb(142, 68,  173),
                Color.FromArgb(39,  174, 96)
            };
            int ci = 0;
            foreach (var kv in catTotals)
            {
                pieSeries.Points.AddXY(kv.Key, kv.Value);
                pieSeries.Points[pieSeries.Points.Count - 1].Color = pieColors[ci++ % pieColors.Length];
            }
            _pie.Series.Add(pieSeries);

            // ── Line: monthly spend trend ─────────────────────────────────
            _lineChart.Series.Clear();
            var monthly = new SortedDictionary<string, double>();
            foreach (var exp in _expenses)
            {
                string[] parts = exp.Date.Split('/');
                if (parts.Length == 3)
                {
                    string key = parts[2] + "/" + parts[1]; // yyyy/MM for sorting
                    if (!monthly.ContainsKey(key)) monthly[key] = 0;
                    monthly[key] += exp.Amount;
                }
            }

            var lineSeries = new Series("Trend")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 2,
                Color = Color.FromArgb(41, 128, 185),
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 6,
                MarkerColor = Color.FromArgb(41, 128, 185)
            };
            foreach (var kv in monthly)
            {
                string[] p = kv.Key.Split('/');
                string label = p.Length == 2
                    ? System.Globalization.CultureInfo.CurrentCulture
                             .DateTimeFormat.GetAbbreviatedMonthName(int.Parse(p[1]))
                      + " " + p[0]
                    : kv.Key;
                lineSeries.Points.AddXY(label, kv.Value);
            }
            _lineChart.Series.Add(lineSeries);
        }

        private void UpdateStatBoxes()
        {
            // Stat boxes are inside a FlowLayoutPanel — update their value labels
            // The flow panel is the first control added after the title bar
            // Each MakeStatBox has two labels; the second one (index 1) is the value
            if (_panelMain == null) return;

            FlowLayoutPanel flow = null;
            foreach (Control c in _panelMain.Controls)
            {
                if (c is FlowLayoutPanel flp) { flow = flp; break; }
            }
            if (flow == null) return;

            double totalSpent = _expenses.Sum(x => x.Amount);
            double balance = 50000; // configurable constant
            double savings = balance - totalSpent;

            string[] values = { $"₹ {balance:N0}", $"₹ {totalSpent:N0}", $"₹ {savings:N0}" };
            int idx = 0;
            foreach (Control box in flow.Controls)
            {
                if (box is Panel p && idx < values.Length)
                {
                    foreach (Control lbl in p.Controls)
                    {
                        if (lbl is Label l && l.Font.Size > 10f)  // value label is 14pt
                            l.Text = values[idx];
                    }
                    idx++;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPER FACTORIES
        // ══════════════════════════════════════════════════════════════════
        private Panel MakeStatBox(string title, string value, Color bg)
        {
            var p = new Panel { Size = new Size(210, 64), BackColor = bg, Margin = new Padding(2) };
            p.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(0, 5),
                Size = new Size(210, 22),
                TextAlign = ContentAlignment.MiddleCenter
            });
            p.Controls.Add(new Label
            {
                Text = value,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(0, 28),
                Size = new Size(210, 30),
                TextAlign = ContentAlignment.MiddleCenter
            });
            return p;
        }

        private Button MakeButton(string text, Color bg, int x, int y)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(165, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private Label MakeFormLabel(string text) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 10f),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        private Panel MakeInsightRow(string key, string value, int y)
        {
            var row = new Panel { Location = new Point(0, y), Size = new Size(638, 36) };
            row.Controls.Add(new Label
            {
                Text = key,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TitleBarColor,
                Location = new Point(12, 0),
                Size = new Size(240, 36),
                TextAlign = ContentAlignment.MiddleLeft
            });
            row.Controls.Add(new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TitleBarColor,
                Location = new Point(252, 0),
                Size = new Size(300, 36),
                TextAlign = ContentAlignment.MiddleLeft
            });
            return row;
        }

        private void MainForm_Load(object sender, EventArgs e) { }

        private void LoadExpensesFromStorage()
        {
            try
            {
                foreach (var exp in _expenseManager.GetAll())
                {
                    _expenses.Add((
                        exp.Date.ToString("dd/MM/yyyy"),
                        exp.Category,
                        exp.Amount,
                        exp.Note ?? ""
                    ));
                }

                if (_expenses.Count > 0)
                {
                    UpdateStatBoxes();
                    UpdateAlerts();
                    UpdateCharts();
                    UpdateInsightsPanel();
                    LoadGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load saved expenses:\n{ex.Message}",
                                "Load Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateInsightsPanel()
        {
            if (_lblTopCategory == null) return;
            if (_expenses.Count == 0)
            {
                _lblTopCategory.Text = "—";
                _lblExpensiveDay.Text = "—";
                _lblAvgDaily.Text = "₹ 0";

                _lstSuggestions.Items.Clear();
                _lstSuggestions.Items.Add("  •  Add expenses to see personalised suggestions.");
                return;
            }

            // ── Top spending category ─────────────────────────────────────────
            var catTotals = new Dictionary<string, double>();
            foreach (var exp in _expenses)
            {
                if (!catTotals.ContainsKey(exp.Category)) catTotals[exp.Category] = 0;
                catTotals[exp.Category] += exp.Amount;
            }
            string topCat = "";
            double topAmt = 0;
            foreach (var kv in catTotals)
                if (kv.Value > topAmt) { topAmt = kv.Value; topCat = kv.Key; }

            _lblTopCategory.Text = $"{topCat}  (₹{topAmt:N0})";

            // ── Most expensive single day ─────────────────────────────────────
            var dayTotals = new Dictionary<string, double>();
            foreach (var exp in _expenses)
            {
                if (!dayTotals.ContainsKey(exp.Date)) dayTotals[exp.Date] = 0;
                dayTotals[exp.Date] += exp.Amount;
            }
            string topDay = "";
            double topDayAmt = 0;
            foreach (var kv in dayTotals)
                if (kv.Value > topDayAmt) { topDayAmt = kv.Value; topDay = kv.Key; }

            _lblExpensiveDay.Text = $"{topDay}  (₹{topDayAmt:N0})";

            // ── Average daily spend ───────────────────────────────────────────
            double avgDaily = dayTotals.Count > 0
                ? _expenses.Sum(e => e.Amount) / dayTotals.Count
                : 0;
            _lblAvgDaily.Text = $"₹ {avgDaily:N0}";

            // ── Suggestions ───────────────────────────────────────────────────
            _lstSuggestions.Items.Clear();
            double totalSpent = _expenses.Sum(e => e.Amount);

            foreach (var kv in catTotals)
            {
                double pct = totalSpent > 0 ? (kv.Value / totalSpent) * 100 : 0;

                if (kv.Key == "Food" && kv.Value > 5000)
                    _lstSuggestions.Items.Add($"  •  Food spending is ₹{kv.Value:N0} — try meal prepping to save.");

                if (kv.Key == "Shopping" && kv.Value > 3000)
                    _lstSuggestions.Items.Add($"  •  Shopping is ₹{kv.Value:N0} — consider a weekly spending cap.");

                if (kv.Key == "Travel" && pct > 25)
                    _lstSuggestions.Items.Add($"  •  Travel is {pct:F0}% of spend — carpooling could help.");

                if (kv.Key == "Bills" && pct > 35)
                    _lstSuggestions.Items.Add($"  •  Bills are {pct:F0}% of spend — review subscriptions.");

                if (pct > 50)
                    _lstSuggestions.Items.Add($"  •  {kv.Key} dominates at {pct:F0}% — consider rebalancing.");
            }

            if (avgDaily > 1000)
                _lstSuggestions.Items.Add($"  •  Daily average ₹{avgDaily:N0} is high — set a daily limit.");

            if (_lstSuggestions.Items.Count == 0)
                _lstSuggestions.Items.Add("  ✅  Spending looks balanced. Keep it up!");
        }

        private void BtnGenerateReport_Click(object sender, EventArgs e)
        {
            MessageBox.Show("PDF report generation coming soon!", "Report",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
