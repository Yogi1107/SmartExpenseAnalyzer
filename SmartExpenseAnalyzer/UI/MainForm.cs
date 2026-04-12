using SmartExpenseAnalyzer.Models;
using SmartExpenseAnalyzer.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace SmartExpenseAnalyzer.UI
{
    public partial class MainForm : Form
    {
        // ── Theme colors ──────────────────────────────────────────────────
        private static readonly Color NavBg = Color.FromArgb(28, 37, 65);
        private static readonly Color NavAccent = Color.FromArgb(70, 130, 180);
        private static readonly Color AccentBlue = Color.FromArgb(70, 130, 180);
        private static readonly Color AccentGreen = Color.FromArgb(39, 174, 96);
        private static readonly Color AccentOrange = Color.FromArgb(211, 84, 0);
        private static readonly Color AccentRed = Color.FromArgb(192, 57, 43);
        private static readonly Color PageBg = Color.FromArgb(240, 244, 250);

        // ── In-memory data store ──────────────────────────────────────────
        private readonly List<(string Date, string Category, double Amount, string Desc)> _expenses
            = new List<(string, string, double, string)>();

        private readonly ExpenseManager _expenseManager = new ExpenseManager();

        // ── Navigation ────────────────────────────────────────────────────
        private Panel _navBar;
        private Panel _contentArea;

        // Cached panels (lazy-built)
        private Panel _pgDashboard;
        private Panel _pgAddExpense;
        private Panel _pgHistory;
        private Panel _pgInsights;

        // Nav buttons for highlight tracking
        private Button _btnNavDash, _btnNavAdd, _btnNavHistory, _btnNavInsights;

        // ── Dashboard controls ────────────────────────────────────────────
        private Label _dshTotalBalance, _dshTotalSpent, _dshSavings;
        private ListBox _dshAlerts;
        private Chart _dshPie, _dshLine;

        // ── Add Expense controls ──────────────────────────────────────────
        private TextBox _txtAmount, _txtDescription;
        private ComboBox _cmbCategory;
        private DateTimePicker _dtpDate;

        // ── History controls ──────────────────────────────────────────────
        private DataGridView _dgvHistory;
        private Label _lblHistTotal;
        private ComboBox _cmbFilterCat, _cmbFilterMonth;
        private TextBox _txtSearch;

        // ── Insights controls ─────────────────────────────────────────────
        private Label _lblTopCategory, _lblExpensiveDay, _lblAvgDaily;
        private ListBox _lstSuggestions;

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════
        public MainForm()
        {
            this.Text = "Smart Expense Analyzer";
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1024, 700);
            this.BackColor = PageBg;
            this.Font = new Font("Segoe UI", 9f);

            BuildShell();          // nav bar + content area
            NavigateTo("Dashboard");
            LoadExpensesFromStorage();
        }

        // ═════════════════════════════════════════════════════════════════
        //  SHELL  (nav bar on the left, content area on the right)
        // ═════════════════════════════════════════════════════════════════
        private void BuildShell()
        {
            // ── Left nav bar ─────────────────────────────────────────────
            _navBar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 210,
                BackColor = NavBg
            };

            // Logo / app name
            var logo = new Label
            {
                Text = "💰 SmartExpense",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 64,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(20, 27, 50)
            };
            _navBar.Controls.Add(logo);

            // Separator
            var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(60, 80, 130) };
            _navBar.Controls.Add(sep);

            // Nav buttons (added top-down; Controls stacks in reverse so add in reverse order)
            _btnNavInsights = MakeNavButton("📊  Insights", "Insights");
            _btnNavHistory = MakeNavButton("🗂   History", "History");
            _btnNavAdd = MakeNavButton("➕  Add Expense", "Add");
            _btnNavDash = MakeNavButton("🏠  Dashboard", "Dashboard");

            // Spacer at bottom
            var spacer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _navBar.Controls.Add(spacer);

            _navBar.Controls.Add(_btnNavInsights);
            _navBar.Controls.Add(_btnNavHistory);
            _navBar.Controls.Add(_btnNavAdd);
            _navBar.Controls.Add(_btnNavDash);

            // ── Content area ──────────────────────────────────────────────
            _contentArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(20)
            };

            this.Controls.Add(_contentArea);
            this.Controls.Add(_navBar);   // added last so it overlaps on left
        }

        private Button MakeNavButton(string label, string target)
        {
            var b = new Button
            {
                Text = label,
                Dock = DockStyle.Top,
                Height = 48,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(190, 210, 240),
                Font = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(24, 0, 0, 0),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 100, 160);
            b.Click += (s, e) => NavigateTo(target);
            return b;
        }

        private void NavigateTo(string page)
        {
            // Highlight active nav button
            foreach (var nb in new[] { _btnNavDash, _btnNavAdd, _btnNavHistory, _btnNavInsights })
            {
                if (nb == null) continue;
                nb.BackColor = Color.Transparent;
                nb.ForeColor = Color.FromArgb(190, 210, 240);
                nb.Font = new Font("Segoe UI", 10f);
            }

            Panel target = null;
            Button active = null;

            switch (page)
            {
                case "Dashboard":
                    if (_pgDashboard == null) _pgDashboard = BuildDashboardPage();
                    target = _pgDashboard; active = _btnNavDash; break;
                case "Add":
                    if (_pgAddExpense == null) _pgAddExpense = BuildAddExpensePage();
                    target = _pgAddExpense; active = _btnNavAdd; break;
                case "History":
                    if (_pgHistory == null) _pgHistory = BuildHistoryPage();
                    target = _pgHistory; active = _btnNavHistory; break;
                case "Insights":
                    if (_pgInsights == null) _pgInsights = BuildInsightsPage();
                    target = _pgInsights; active = _btnNavInsights; break;
            }

            if (active != null)
            {
                active.BackColor = NavAccent;
                active.ForeColor = Color.White;
                active.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            }

            if (target == null) return;

            // Show the target page, hide others
            foreach (var pg in new[] { _pgDashboard, _pgAddExpense, _pgHistory, _pgInsights })
                if (pg != null) pg.Visible = false;

            if (!_contentArea.Controls.Contains(target))
                _contentArea.Controls.Add(target);

            target.Dock = DockStyle.Fill;
            target.Visible = true;
            target.BringToFront();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PAGE 1 — DASHBOARD
        // ═════════════════════════════════════════════════════════════════
        private Panel BuildDashboardPage()
        {
            var page = new Panel { BackColor = PageBg };

            // ── Page header ───────────────────────────────────────────────
            var hdr = PageHeader("Dashboard", "Your financial overview at a glance");
            page.Controls.Add(hdr);

            // ── Stat cards (3 across) ─────────────────────────────────────
            var cardRow = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 72),
                Height = 100,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var cardBalance = MakeStatCard("Total Balance", "₹ 50,000", "💳", AccentBlue);
            var cardSpent = MakeStatCard("Total Spent", "₹ 0", "📤", AccentOrange);
            var cardSavings = MakeStatCard("Savings", "₹ 50,000", "💰", AccentGreen);

            // Grab value labels for later update
            _dshTotalBalance = FindValueLabel(cardBalance);
            _dshTotalSpent = FindValueLabel(cardSpent);
            _dshSavings = FindValueLabel(cardSavings);

            cardRow.Controls.AddRange(new[] { cardBalance, cardSpent, cardSavings });
            page.Controls.Add(cardRow);

            // Distribute card widths on resize
            page.Resize += (s, e) =>
            {
                cardRow.Width = page.ClientSize.Width;
                int cardW = Math.Max(80, (page.ClientSize.Width - 30) / 3);
                foreach (Control c in cardRow.Controls)
                    c.Width = cardW;
            };

            // ── Quick-action buttons ───────────────────────────────────────
            var quickRow = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 182),
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var qAdd = MakePillButton("➕  Add Expense", AccentBlue);
            var qHistory = MakePillButton("🗂   View History", AccentBlue);
            var qInsights = MakePillButton("📊  Insights", AccentGreen);

            qAdd.Click += (s, e) => NavigateTo("Add");
            qHistory.Click += (s, e) => NavigateTo("History");
            qInsights.Click += (s, e) => NavigateTo("Insights");

            quickRow.Controls.AddRange(new[] { qAdd, qHistory, qInsights });
            page.Controls.Add(quickRow);

            // ── Charts row ────────────────────────────────────────────────
            var chartsRow = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(0, 242),
                BackColor = Color.Transparent
            };

            // Pie chart card
            var pieCard = CardPanel("Spending by Category");
            _dshPie = new Chart { Dock = DockStyle.Fill, BackColor = Color.White };
            var pieArea = new ChartArea { BackColor = Color.White };
            _dshPie.ChartAreas.Add(pieArea);
            _dshPie.Legends.Add(new Legend { Docking = Docking.Right, BackColor = Color.White });
            pieCard.Controls.Add(_dshPie);
            chartsRow.Controls.Add(pieCard);

            // Line chart card
            var lineCard = CardPanel("Monthly Trend");
            _dshLine = new Chart { Dock = DockStyle.Fill, BackColor = Color.White };
            var lineArea = new ChartArea { BackColor = Color.White };
            lineArea.AxisX.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            lineArea.AxisY.MajorGrid.LineColor = Color.FromArgb(230, 230, 230);
            _dshLine.ChartAreas.Add(lineArea);
            lineCard.Controls.Add(_dshLine);
            chartsRow.Controls.Add(lineCard);

            chartsRow.Resize += (s, e) =>
            {
                int w = chartsRow.ClientSize.Width;
                int h = chartsRow.ClientSize.Height;
                int half = (w - 8) / 2;
                pieCard.SetBounds(0, 0, half, h);
                lineCard.SetBounds(half + 8, 0, w - half - 8, h);
            };

            page.Controls.Add(chartsRow);

            // ── Alerts card (bottom strip) ────────────────────────────────
            var alertCard = new Panel
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Height = 120,
                BackColor = Color.White
            };
            alertCard.Paint += CardPaint;

            var alertHdr = new Label
            {
                Text = "⚡ Alerts & Insights",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AccentBlue,
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(12, 6, 0, 0)
            };
            alertCard.Controls.Add(alertHdr);

            _dshAlerts = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White
            };
            _dshAlerts.Items.Add("ℹ  No expenses recorded yet. Add your first expense!");
            alertCard.Controls.Add(_dshAlerts);
            page.Controls.Add(alertCard);

            // ── Layout resize ─────────────────────────────────────────────
            const int alertH = 120;
            page.Resize += (s, e) =>
            {
                int w = page.ClientSize.Width;
                int h = page.ClientSize.Height;
                int top = 242;
                int available = h - top - alertH - 8;
                chartsRow.SetBounds(0, top, w, Math.Max(60, available));
                alertCard.SetBounds(0, h - alertH, w, alertH);
            };

            return page;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PAGE 2 — ADD EXPENSE
        // ═════════════════════════════════════════════════════════════════
        private Panel BuildAddExpensePage()
        {
            var page = new Panel { BackColor = PageBg };

            var hdr = PageHeader("Add New Expense", "Record a new transaction");
            page.Controls.Add(hdr);

            // Card wrapper
            var card = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 80),
                BackColor = Color.White,
                Padding = new Padding(24)
            };
            card.Paint += CardPaint;

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 240,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = Color.White,
                Padding = new Padding(0, 8, 0, 0)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

            tbl.Controls.Add(FormLabel("Amount (₹):"), 0, 0);
            _txtAmount = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4), Font = new Font("Segoe UI", 11f) };
            tbl.Controls.Add(_txtAmount, 1, 0);

            tbl.Controls.Add(FormLabel("Category:"), 0, 1);
            _cmbCategory = new ComboBox { Dock = DockStyle.Fill, Margin = new Padding(4), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11f) };
            _cmbCategory.Items.AddRange(new[] { "Food", "Travel", "Shopping", "Bills" });
            _cmbCategory.SelectedIndex = 0;
            tbl.Controls.Add(_cmbCategory, 1, 1);

            tbl.Controls.Add(FormLabel("Date:"), 0, 2);
            _dtpDate = new DateTimePicker { Dock = DockStyle.Fill, Margin = new Padding(4), Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 11f), Value = DateTime.Today };
            tbl.Controls.Add(_dtpDate, 1, 2);

            tbl.Controls.Add(FormLabel("Description:"), 0, 3);
            _txtDescription = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4), Font = new Font("Segoe UI", 11f) };
            tbl.Controls.Add(_txtDescription, 1, 3);

            card.Controls.Add(tbl);

            // Button row
            var btnRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 56,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.White
            };
            var btnSave = MakePillButton("💾  Save Expense", AccentBlue);
            var btnClear = MakePillButton("✖  Clear", Color.FromArgb(140, 140, 140));
            var btnGoBack = MakePillButton("← Dashboard", AccentGreen);

            btnSave.Click += BtnSave_Click;
            btnClear.Click += (s, e) => { _txtAmount.Clear(); _txtDescription.Clear(); _cmbCategory.SelectedIndex = 0; _dtpDate.Value = DateTime.Today; };
            btnGoBack.Click += (s, e) => NavigateTo("Dashboard");

            btnRow.Controls.AddRange(new[] { btnSave, btnClear, btnGoBack });
            card.Controls.Add(btnRow);

            // Card resizes with page
            page.Resize += (s, e) =>
            {
                card.Width = Math.Min(700, page.ClientSize.Width);
                card.Height = 330;
            };

            page.Controls.Add(card);

            // Success label (hidden until save)
            var lblOk = new Label
            {
                Text = "✅  Expense saved successfully!",
                ForeColor = AccentGreen,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Location = new Point(0, 420),
                AutoSize = true,
                Visible = false
            };
            page.Controls.Add(lblOk);
            btnSave.Tag = lblOk;   // passed to handler via Tag

            return page;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PAGE 3 — EXPENSE HISTORY
        // ═════════════════════════════════════════════════════════════════
        private Panel BuildHistoryPage()
        {
            var page = new Panel { BackColor = PageBg };

            var hdr = PageHeader("Expense History", "Browse and filter all your transactions");
            page.Controls.Add(hdr);

            // Filter bar card
            var filterCard = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 80),
                Height = 56,
                BackColor = Color.White,
                Padding = new Padding(12, 10, 12, 0)
            };
            filterCard.Paint += CardPaint;

            var filterFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.White
            };

            filterFlow.Controls.Add(new Label { Text = "Filter:", AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(0, 4, 8, 0) });

            _cmbFilterCat = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 0, 8, 0) };
            _cmbFilterCat.Items.AddRange(new[] { "All Categories", "Food", "Travel", "Shopping", "Bills" });
            _cmbFilterCat.SelectedIndex = 0;
            _cmbFilterCat.SelectedIndexChanged += (s, e) => ApplyFilters();

            _cmbFilterMonth = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 0, 8, 0) };
            _cmbFilterMonth.Items.AddRange(new[] { "All", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" });
            _cmbFilterMonth.SelectedIndex = 0;
            _cmbFilterMonth.SelectedIndexChanged += (s, e) => ApplyFilters();

            _txtSearch = new TextBox { Width = 160, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 0, 8, 0) };
            _txtSearch.TextChanged += (s, e) => ApplyFilters();

            filterFlow.Controls.AddRange(new Control[] { _cmbFilterCat, _cmbFilterMonth, _txtSearch });
            filterCard.Controls.Add(filterFlow);
            page.Controls.Add(filterCard);

            // Grid card
            var gridCard = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(0, 148),
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            gridCard.Paint += CardPaint;

            _dgvHistory = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9f),
                GridColor = Color.FromArgb(230, 235, 242)
            };
            _dgvHistory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 252);
            _dgvHistory.ColumnHeadersDefaultCellStyle.ForeColor = AccentBlue;
            _dgvHistory.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _dgvHistory.EnableHeadersVisualStyles = false;
            _dgvHistory.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 255);

            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 16 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 16 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount (₹)", FillWeight = 14 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "Desc", HeaderText = "Description", FillWeight = 54 });

            gridCard.Controls.Add(_dgvHistory);

            _lblHistTotal = new Label
            {
                Text = "Total:  ₹ 0",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Dock = DockStyle.Bottom,
                Height = 32,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 16, 0),
                BackColor = Color.FromArgb(245, 247, 252)
            };
            gridCard.Controls.Add(_lblHistTotal);

            page.Controls.Add(gridCard);

            page.Resize += (s, e) =>
            {
                int w = page.ClientSize.Width;
                filterCard.Width = w;
                gridCard.SetBounds(0, 148, w, Math.Max(80, page.ClientSize.Height - 148));
            };

            LoadGrid();
            return page;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PAGE 4 — SPENDING INSIGHTS
        // ═════════════════════════════════════════════════════════════════
        private Panel BuildInsightsPage()
        {
            var page = new Panel { BackColor = PageBg };

            var hdr = PageHeader("Spending Insights", "Understand where your money goes");
            page.Controls.Add(hdr);

            // ── Insight stats row ─────────────────────────────────────────
            var statsRow = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 80),
                Height = 110,
                BackColor = Color.Transparent
            };

            var cardTop = InsightCard("🏆 Top Category", "—", AccentBlue);
            var cardDay = InsightCard("📅 Most Expensive Day", "—", AccentOrange);
            var cardAvg = InsightCard("📈 Average Daily Spend", "₹ 0", AccentGreen);

            _lblTopCategory = FindValueLabel(cardTop);
            _lblExpensiveDay = FindValueLabel(cardDay);
            _lblAvgDaily = FindValueLabel(cardAvg);

            statsRow.Controls.AddRange(new[] { cardTop, cardDay, cardAvg });
            statsRow.Resize += (s, e) =>
            {
                int w = statsRow.ClientSize.Width;
                int cardW = Math.Max(60, (w - 16) / 3);
                cardTop.SetBounds(0, 0, cardW, statsRow.ClientSize.Height);
                cardDay.SetBounds(cardW + 8, 0, cardW, statsRow.ClientSize.Height);
                cardAvg.SetBounds((cardW + 8) * 2, 0, w - (cardW + 8) * 2, statsRow.ClientSize.Height);
            };

            page.Controls.Add(statsRow);

            // ── Suggestions card ──────────────────────────────────────────
            var sugCard = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(0, 202),
                BackColor = Color.White
            };
            sugCard.Paint += CardPaint;

            var sugHdr = new Label
            {
                Text = "💡 Personalised Suggestions",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AccentBlue,
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(14, 10, 0, 0)
            };
            sugCard.Controls.Add(sugHdr);

            _lstSuggestions = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ItemHeight = 28
            };
            _lstSuggestions.Items.Add("  •  Add expenses to see personalised suggestions.");
            sugCard.Controls.Add(_lstSuggestions);

            page.Controls.Add(sugCard);

            // Generate report button
            var btnReport = MakePillButton("📄  Generate Report PDF", AccentBlue);
            btnReport.Anchor = AnchorStyles.Bottom | AnchorStyles.None;
            btnReport.Click += (s, e) => MessageBox.Show("PDF report generation coming soon!", "Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
            page.Controls.Add(btnReport);

            page.Resize += (s, e) =>
            {
                int w = page.ClientSize.Width;
                int h = page.ClientSize.Height;
                statsRow.SetBounds(0, 80, w, 110);
                sugCard.SetBounds(0, 202, w, Math.Max(60, h - 202 - 60));
                btnReport.Location = new Point((w - btnReport.Width) / 2, h - 52);
            };

            return page;
        }

        // ═════════════════════════════════════════════════════════════════
        //  BUSINESS LOGIC
        // ═════════════════════════════════════════════════════════════════
        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(_txtAmount.Text.Trim(), out double amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var expense = new Expense
            {
                Amount = amount,
                Category = _cmbCategory.SelectedItem.ToString(),
                Date = _dtpDate.Value,
                Note = _txtDescription.Text.Trim()
            };

            try
            {
                _expenseManager.Add(expense);
                _expenses.Add((expense.Date.ToString("dd/MM/yyyy"), expense.Category, expense.Amount, expense.Note));

                RefreshAllPanels();

                _txtAmount.Clear();
                _txtDescription.Clear();
                _cmbCategory.SelectedIndex = 0;
                _dtpDate.Value = DateTime.Today;

                // Show success label if wired up
                if (sender is Button b && b.Tag is Label lbl)
                {
                    lbl.Visible = true;
                    var t = new System.Windows.Forms.Timer { Interval = 2500 };
                    t.Tick += (ts, te) => { lbl.Visible = false; t.Stop(); t.Dispose(); };
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save expense:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshAllPanels()
        {
            UpdateDashboard();
            if (_pgHistory != null) LoadGrid();
            if (_pgInsights != null) UpdateInsights();
        }

        private void UpdateDashboard()
        {
            if (_pgDashboard == null) return;
            // Stat cards
            double totalSpent = _expenses.Sum(x => x.Amount);
            double balance = 50000;
            double savings = balance - totalSpent;

            if (_dshTotalBalance != null) _dshTotalBalance.Text = $"₹ {balance:N0}";
            if (_dshTotalSpent != null) _dshTotalSpent.Text = $"₹ {totalSpent:N0}";
            if (_dshSavings != null) _dshSavings.Text = $"₹ {savings:N0}";

            // Alerts
            if (_dshAlerts != null)
            {
                _dshAlerts.Items.Clear();
                if (_expenses.Count == 0) { _dshAlerts.Items.Add("ℹ  No expenses recorded yet. Add your first expense!"); }
                else
                {
                    var cat = new Dictionary<string, double>();
                    foreach (var exp in _expenses) { if (!cat.ContainsKey(exp.Category)) cat[exp.Category] = 0; cat[exp.Category] += exp.Amount; }

                    if (cat.TryGetValue("Food", out double food) && food > 5000) _dshAlerts.Items.Add("⚠  You overspent on Food this month!");
                    if (cat.TryGetValue("Shopping", out double shop) && shop > 3000) _dshAlerts.Items.Add("⚠  Shopping budget exceeded!");
                    foreach (var kv in cat) { double pct = totalSpent > 0 ? (kv.Value / totalSpent) * 100 : 0; if (pct > 40) _dshAlerts.Items.Add($"💡  {kv.Key} is {pct:F0}% of spending — consider reducing."); }
                    if (_dshAlerts.Items.Count == 0) _dshAlerts.Items.Add("✅  All spending within budget. Great job!");
                }
            }

            // Charts
            if (_dshPie != null && _dshLine != null)
            {
                _dshPie.Series.Clear();
                var pieSeries = new Series("Cat") { ChartType = SeriesChartType.Pie, IsValueShownAsLabel = false };
                pieSeries["PieStartAngle"] = "270";
                var catTotals = new Dictionary<string, double>();
                foreach (var exp in _expenses) { if (!catTotals.ContainsKey(exp.Category)) catTotals[exp.Category] = 0; catTotals[exp.Category] += exp.Amount; }
                Color[] pieColors = { Color.FromArgb(231, 76, 60), Color.FromArgb(41, 128, 185), Color.FromArgb(230, 126, 34), Color.FromArgb(142, 68, 173), Color.FromArgb(39, 174, 96) };
                int ci = 0;
                foreach (var kv in catTotals) { pieSeries.Points.AddXY(kv.Key, kv.Value); pieSeries.Points[pieSeries.Points.Count - 1].Color = pieColors[ci++ % pieColors.Length]; }
                _dshPie.Series.Add(pieSeries);

                _dshLine.Series.Clear();
                var monthly = new SortedDictionary<string, double>();
                foreach (var exp in _expenses)
                {
                    string[] parts = exp.Date.Split('/');
                    if (parts.Length == 3) { string key = parts[2] + "/" + parts[1]; if (!monthly.ContainsKey(key)) monthly[key] = 0; monthly[key] += exp.Amount; }
                }
                var lineSeries = new Series("Trend") { ChartType = SeriesChartType.Line, BorderWidth = 2, Color = Color.FromArgb(41, 128, 185), MarkerStyle = MarkerStyle.Circle, MarkerSize = 6, MarkerColor = Color.FromArgb(41, 128, 185) };
                foreach (var kv in monthly)
                {
                    string[] p = kv.Key.Split('/');
                    string label = p.Length == 2 ? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(int.Parse(p[1])) + " " + p[0] : kv.Key;
                    lineSeries.Points.AddXY(label, kv.Value);
                }
                _dshLine.Series.Add(lineSeries);
            }
        }

        private void LoadGrid()
        {
            if (_dgvHistory == null) return;

            string catFilter = _cmbFilterCat?.SelectedItem?.ToString() ?? "All Categories";
            string monthFilter = _cmbFilterMonth?.SelectedItem?.ToString() ?? "All";
            string search = _txtSearch?.Text?.Trim() ?? "";

            var filtered = _expenses.Where(exp =>
            {
                bool passCat = catFilter == "All Categories" || exp.Category == catFilter;

                bool passMonth = monthFilter == "All";
                if (!passMonth)
                {
                    string[] parts = exp.Date.Split('/');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int mNum))
                    {
                        string mName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(mNum);
                        passMonth = string.Equals(mName, monthFilter, StringComparison.OrdinalIgnoreCase);
                    }
                }

                bool passSearch = string.IsNullOrEmpty(search)
                    || exp.Desc.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || exp.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

                return passCat && passMonth && passSearch;
            }).Select(x => new { x.Date, x.Category, x.Amount, x.Desc }).ToList();

            _dgvHistory.DataSource = null;
            _dgvHistory.DataSource = filtered;

            if (_lblHistTotal != null) _lblHistTotal.Text = $"Total:  ₹ {filtered.Sum(x => x.Amount):N0}";
        }

        private void ApplyFilters() => LoadGrid();

        private void UpdateInsights()
        {
            if (_lblTopCategory == null) return;
            if (_expenses.Count == 0)
            {
                _lblTopCategory.Text = "—"; _lblExpensiveDay.Text = "—"; _lblAvgDaily.Text = "₹ 0";
                _lstSuggestions.Items.Clear(); _lstSuggestions.Items.Add("  •  Add expenses to see personalised suggestions.");
                return;
            }

            var catTotals = new Dictionary<string, double>();
            foreach (var exp in _expenses) { if (!catTotals.ContainsKey(exp.Category)) catTotals[exp.Category] = 0; catTotals[exp.Category] += exp.Amount; }

            string topCat = ""; double topAmt = 0;
            foreach (var kv in catTotals) if (kv.Value > topAmt) { topAmt = kv.Value; topCat = kv.Key; }
            _lblTopCategory.Text = $"{topCat}  (₹{topAmt:N0})";

            var dayTotals = new Dictionary<string, double>();
            foreach (var exp in _expenses) { if (!dayTotals.ContainsKey(exp.Date)) dayTotals[exp.Date] = 0; dayTotals[exp.Date] += exp.Amount; }
            string topDay = ""; double topDayAmt = 0;
            foreach (var kv in dayTotals) if (kv.Value > topDayAmt) { topDayAmt = kv.Value; topDay = kv.Key; }
            _lblExpensiveDay.Text = $"{topDay}  (₹{topDayAmt:N0})";

            double totalSpent = _expenses.Sum(e => e.Amount);
            double avgDaily = dayTotals.Count > 0 ? totalSpent / dayTotals.Count : 0;
            _lblAvgDaily.Text = $"₹ {avgDaily:N0}";

            _lstSuggestions.Items.Clear();
            foreach (var kv in catTotals)
            {
                double pct = totalSpent > 0 ? (kv.Value / totalSpent) * 100 : 0;
                if (kv.Key == "Food" && kv.Value > 5000) _lstSuggestions.Items.Add($"  •  Food spending is ₹{kv.Value:N0} — try meal prepping to save.");
                if (kv.Key == "Shopping" && kv.Value > 3000) _lstSuggestions.Items.Add($"  •  Shopping is ₹{kv.Value:N0} — consider a weekly spending cap.");
                if (kv.Key == "Travel" && pct > 25) _lstSuggestions.Items.Add($"  •  Travel is {pct:F0}% of spend — carpooling could help.");
                if (kv.Key == "Bills" && pct > 35) _lstSuggestions.Items.Add($"  •  Bills are {pct:F0}% of spend — review subscriptions.");
                if (pct > 50) _lstSuggestions.Items.Add($"  •  {kv.Key} dominates at {pct:F0}% — consider rebalancing.");
            }
            if (avgDaily > 1000) _lstSuggestions.Items.Add($"  •  Daily average ₹{avgDaily:N0} is high — set a daily limit.");
            if (_lstSuggestions.Items.Count == 0) _lstSuggestions.Items.Add("  ✅  Spending looks balanced. Keep it up!");
        }

        private void LoadExpensesFromStorage()
        {
            try
            {
                foreach (var exp in _expenseManager.GetAll())
                    _expenses.Add((exp.Date.ToString("dd/MM/yyyy"), exp.Category, exp.Amount, exp.Note ?? ""));

                if (_expenses.Count > 0) RefreshAllPanels();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load saved expenses:\n{ex.Message}", "Load Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  UI HELPER FACTORIES
        // ═════════════════════════════════════════════════════════════════

        // Page header with title + subtitle
        private Panel PageHeader(string title, string sub)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.Transparent };
            p.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = NavBg, Location = new Point(0, 8), AutoSize = true });
            p.Controls.Add(new Label { Text = sub, Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(110, 130, 160), Location = new Point(2, 42), AutoSize = true });
            return p;
        }

        // Flat card panel with shadow via Paint
        private Panel CardPanel(string title)
        {
            var p = new Panel { BackColor = Color.White };
            p.Paint += CardPaint;
            if (!string.IsNullOrEmpty(title))
                p.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = AccentBlue, Location = new Point(12, 8), AutoSize = true });
            return p;
        }

        private void CardPaint(object sender, PaintEventArgs e)
        {
            var p = (Panel)sender;
            using (var pen = new Pen(Color.FromArgb(220, 228, 240), 1))
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        }

        // Stat card for Dashboard (colored background)
        private Panel MakeStatCard(string title, string value, string icon, Color bg)
        {
            var p = new Panel { Height = 100, BackColor = bg, Margin = new Padding(4) };
            p.Controls.Add(new Label { Text = icon, Font = new Font("Segoe UI", 18f), ForeColor = Color.FromArgb(220, 255, 220), Location = new Point(12, 8), AutoSize = true });
            p.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(220, 240, 255), Location = new Point(14, 38), AutoSize = true });
            // value label — tagged for later lookup
            var val = new Label { Text = value, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(14, 58), AutoSize = true, Tag = "value" };
            p.Controls.Add(val);
            return p;
        }

        // Insight card (white background, colored accent strip)
        private Panel InsightCard(string title, string value, Color accent)
        {
            var p = new Panel { BackColor = Color.White };
            p.Paint += CardPaint;

            // Accent left bar
            var bar = new Panel { BackColor = accent, Width = 4, Dock = DockStyle.Left };
            p.Controls.Add(bar);

            p.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(100, 120, 160), Location = new Point(16, 12), AutoSize = true });
            var val = new Label { Text = value, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = NavBg, Location = new Point(16, 36), AutoSize = true, Tag = "value" };
            p.Controls.Add(val);
            return p;
        }

        // Grab the label tagged "value" inside a card
        private Label FindValueLabel(Panel card)
        {
            foreach (Control c in card.Controls)
                if (c is Label l && l.Tag?.ToString() == "value") return l;
            return null;
        }

        private Button MakePillButton(string text, Color bg)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(180, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private Label FormLabel(string text) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = NavBg,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
    }
}