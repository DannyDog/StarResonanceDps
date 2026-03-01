using AntdUI;
using StarResonanceDpsAnalysis.Effects;
using StarResonanceDpsAnalysis.Forms;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Plugin.Charts;
using StarResonanceDpsAnalysis.Plugin.DamageStatistics;
using StarResonanceDpsAnalysis.Properties;
using static StarResonanceDpsAnalysis.Forms.DpsStatisticsForm;

namespace StarResonanceDpsAnalysis.Control
{
    public partial class SkillDetailForm : BorderlessForm
    {

        // 新增：上下文类型（默认保持你原来的行为 = 当前战斗）
        public DetailContextType ContextType { get; set; } = DetailContextType.Current;

        // 新增：快照模式下的起始时间（用 StartedAt 精确定位快照）
        public DateTime? SnapshotStartTime { get; set; } = null;


        // 添加折线图成员变量
        private FlatLineChart _dpsTrendChart;
        // 添加条形图和饼图成员变量
        private FlatBarChart _skillDistributionChart;
        private FlatPieChart _critLuckyChart;

        // 添加缺失的isSelect变量
        bool isSelect = false;

        // 添加分割器动态调整相关变量
        private int _lastSplitterPosition = 350; // 记录上次分割器位置，与Designer中的默认值保持一致
        private const int SPLITTER_STEP_PIXELS = 30; // 每30像素触发一次调整
        private const int PADDING_ADJUSTMENT = 15;   // 每次调整PaddingRight的数值

        private void SetDefaultFontFromResources()
        {

            TitleText.Font = AppConfig.TitleFont;
            label1.Font = AppConfig.HeaderFont;
            label2.Font = label3.Font = label4.Font = AppConfig.ContentFont;

            var harmonyOsSansFont_Size11 = HandledResources.GetHarmonyOS_SansFont(11);
            label3.Font = label9.Font = harmonyOsSansFont_Size11;

            var harmonyOsSansFont_Size12 = HandledResources.GetHarmonyOS_SansFont(12);
            NickNameText.Font = harmonyOsSansFont_Size12;

            var digitalFontsControls = new List<System.Windows.Forms.Control>()
            {
                BeatenLabel, AvgDamageText, LuckyDamageText, LuckyTimesLabel,
                CritDamageText, NormalDamageText, NumberCriticalHitsLabel, LuckyRate,
                CritRateText, NumberHitsLabel, TotalDpsText, TotalDamageText
            };
            foreach (var c in digitalFontsControls)
            {
                c.Font = AppConfig.DigitalFont;
            }

            var contentFontControls = new List<System.Windows.Forms.Control>()
            {
                table_DpsDetailDataTable, label13, label14, label1, label2,
                label4, label5, label6, label7, label8, label9, label17,
                NumberCriticalHitsText, UidText, PowerText, segmented1, collapse1,
                label10,label19
            };
            foreach (var c in contentFontControls)
            {
                c.Font = AppConfig.ContentFont;
            }
        }

        public SkillDetailForm()
        {
            InitializeComponent();
            FormGui.SetDefaultGUI(this);
            SetDefaultFontFromResources();


            ToggleTableView();

            ApplyLocalization();

        }

        private int fixedWidth = 1911;//窗体宽度
        private void SkillDetailForm_Load(object sender, EventArgs e)
        {
            FormGui.SetColorMode(this, AppConfig.IsLight);//设置窗体颜色

            isSelect = true;
            select1.Items = new AntdUI.BaseCollection() { Strings.SkillDetail_Select1_Item_SortByDamage, Strings.SkillDetail_Select1_Item_SortByDpsTaken, Strings.SkillDetail_Select1_Item_SortByHitCount, Strings.SkillDetail_Select1_Item_SortByCritRate };
            select1.SelectedIndex = 0;
            isSelect = false;

            // 初始化并添加折线图到panel7
            InitializeDpsTrendChart();

            // 初始化并添加条形图和饼图
            InitializeSkillDistributionChart();
            InitializeCritLuckyChart();

            // 订阅panel7的Resize事件以确保图表正确调整大小
            collapseItem1.Resize += Panel7_Resize;

            // 手动绑定splitter1事件，确保事件处理正确
            splitter1.SplitterMoving += splitter1_SplitterMoving;
            splitter1.SplitterMoved += splitter1_SplitterMoved;

            // 设置分割器的最小位置为350，防止向左拖动
            splitter1.Panel1MinSize = 350;

            // 初始化分割器位置跟踪，确保与实际位置同步
            _lastSplitterPosition = splitter1.SplitterDistance;

            // 确保图表初始状态正确 - 基准位置350时，PaddingRight=160，垂直线条=5
            if (_dpsTrendChart != null)
            {
                var offsetFrom350 = splitter1.SplitterDistance - 350;
                var steps = offsetFrom350 / SPLITTER_STEP_PIXELS;
                var initialPadding = Math.Max(10, Math.Min(300, 160 - steps * PADDING_ADJUSTMENT));
                var initialGridLines = Math.Max(3, Math.Min(10, 5 + steps)); // 修改：将最大值从20改为10

                _dpsTrendChart.SetPaddingRight(initialPadding);
                _dpsTrendChart.SetVerticalGridLines(initialGridLines);

                //Console.WriteLine($"初始化图表 - 分割器位置: {splitter1.SplitterDistance}, PaddingRight: {initialPadding}, 垂直线条: {initialGridLines}");
            }

            ApplyLocalization();

            EnsureTopMost();
        }
        private void EnsureTopMost()
        {
            TopMost = false;   // 先关再开，强制触发样式刷新
            TopMost = true;
            Activate();
            // BringToFront();
        }

        /// <summary>
        /// 设置折线图刷新回调（避免重复代码）
        /// </summary>
        private void SetupTrendChartRefreshCallback()
        {
            if (_dpsTrendChart == null) return;

            _dpsTrendChart.SetRefreshCallback(() =>
            {
                try
                {
                    var dataType = segmented1.SelectIndex switch
                    {
                        0 => ChartDataType.Damage,
                        1 => ChartDataType.Healing,
                        2 => ChartDataType.TakenDamage,
                        _ => ChartDataType.Damage
                    };

                    var source = FormManager.showTotal ? ChartDataSource.FullRecord : ChartDataSource.Current;
                    ChartVisualizationService.RefreshDpsTrendChart(_dpsTrendChart, Uid, dataType, source);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"图表刷新回调出错: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// panel7大小变化时的处理
        /// </summary>
        private void Panel7_Resize(object sender, EventArgs e)
        {
            if (_dpsTrendChart != null)
            {
                try
                {
                    // 由于使用了Dock.Fill，图表会自动调整大小
                    // 这里只需要延迟重绘以确保布局完成后再刷新
                    var resizeTimer = new System.Windows.Forms.Timer { Interval = 100 };
                    resizeTimer.Tick += (s, args) =>
                    {
                        resizeTimer.Stop();
                        resizeTimer.Dispose();

                        if (_dpsTrendChart != null && !_dpsTrendChart.IsDisposed)
                        {
                            _dpsTrendChart.Invalidate();
                        }
                    };
                    resizeTimer.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"调整图表大小时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 初始化DPS趋势图表
        /// </summary>
        private void InitializeDpsTrendChart()
        {
            try
            {
                // 清空panel7现有控件
                collapseItem1.Controls.Clear();

                // 确保panel7大小正确设置并支持自动调整
                collapseItem1.MinimumSize = new Size(ChartConfigManager.MIN_WIDTH, ChartConfigManager.MIN_HEIGHT);
                collapseItem1.Anchor = AnchorStyles.Top | AnchorStyles.Left;

                // 创建DPS趋势折线图，使用统一的配置管理（默认跟随全局数据源）
                _dpsTrendChart = ChartVisualizationService.CreateDpsTrendChart(specificPlayerId: Uid);

                // 设置图表为自适应大小，与其他两个图表保持一致
                _dpsTrendChart.Dock = DockStyle.Fill;

                // 设置实时刷新回调，传入当前玩家ID
                SetupTrendChartRefreshCallback();

                // 添加到panel7
                collapseItem1.Controls.Add(_dpsTrendChart);

                // 确保图表被正确添加后再刷新数据
                Application.DoEvents(); // 让UI更新完成

                // 初始刷新图表数据
                RefreshDpsTrendChart();
            }
            catch (Exception ex)
            {
                // 如果图表初始化失败，显示错误信息
                var errorLabel = new AntdUI.Label
                {
                    Text = $"图表初始化失败: {ex.Message}",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.Red,
                    Font = new Font("Microsoft YaHei", 10, FontStyle.Regular)
                };
                collapseItem1.Controls.Add(errorLabel);

                Console.WriteLine($"图表初始化失败: {ex}");
            }
        }

        /// <summary>
        /// 刷新DPS趋势图表数据
        /// </summary>
        private void RefreshDpsTrendChart()
        {
            if (_dpsTrendChart != null)
            {
                try
                {
                    var dataType = segmented1.SelectIndex switch
                    {
                        0 => ChartDataType.Damage,      // 伤害
                        1 => ChartDataType.Healing,     // 治疗
                        2 => ChartDataType.TakenDamage, // 承伤
                        _ => ChartDataType.Damage       // 默认伤害
                    };

                    var source = FormManager.showTotal ? ChartDataSource.FullRecord : ChartDataSource.Current;
                    ChartVisualizationService.RefreshDpsTrendChart(_dpsTrendChart, Uid, dataType, source);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"刷新DPS趋势图表时出错: {ex.Message}");
                }
            }
        }

        private bool _suspendUiUpdate = false;

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_suspendUiUpdate) return;

            SelectDataType();

            // 图表现在有自己的实时刷新机制，这里只做必要的数据更新检查
            // RefreshDpsTrendChart(); // 移除手动刷新，由图表内部处理
        }

        private void segmented1_SelectIndexChanged(object sender, IntEventArgs e)
        {
            select1.Items.Clear();
            isSelect = true;
            label3.Text = Strings.SkillDetail_Label3;
            label1.Text = Strings.SkillDetail_Label1_TotalDamage;
            label2.Text = Strings.SkillDetail_Label2_DPS;
            label4.Text = Strings.SkillDetail_Label4_Damage_Crit;
            label5.Text = Strings.SkillDetail_Label5_Damage_Lucky;
            switch (e.Value)
            {
                case 0:
                    select1.Items = new AntdUI.BaseCollection() { Strings.SkillDetail_Select1_Item_SortByDamage, Strings.SkillDetail_Select1_Item_SortByDps, Strings.SkillDetail_Select1_Item_SortByHitCount, Strings.SkillDetail_Select1_Item_SortByCritRate };
                    break;
                case 1:
                    select1.Items = new AntdUI.BaseCollection() { Strings.SkillDetail_Select1_Item_SortByHealing, Strings.SkillDetail_Select1_Item_SortByHps, Strings.SkillDetail_Select1_Item_SortByHitCount_Healing, Strings.SkillDetail_Select1_Item_SortByCritRate_Healing };
                    label3.Text = Strings.SkillDetail_Label3_HealingInfo;
                    label1.Text = Strings.SkillDetail_Label1_TotalHealing;
                    label2.Text = Strings.SkillDetail_Label2_HPS;
                    label4.Text = Strings.SkillDetail_Label4_Healing_Crit;
                    label5.Text = Strings.SkillDetail_Label5_Healing_Lucky;
                    break;
                case 2:
                    select1.Items = new AntdUI.BaseCollection() { Strings.SkillDetail_Select1_Item_SortByDamageTaken, Strings.SkillDetail_Select1_Item_SortByDpsTaken, Strings.SkillDetail_Select1_Item_SortByHitCount_Taken, Strings.SkillDetail_Select1_Item_SortByCritRate_Taken };
                    label3.Text = Strings.SkillDetail_Label3_Damage_Taken_Info;
                    label1.Text = Strings.SkillDetail_Label1_Total_Damage_Taken;
                    label2.Text = Strings.SkillDetail_Label2_DPS_Taken;
                    label4.Text = Strings.SkillDetail_Label4_Max_Damage_Taken;
                    label5.Text = Strings.SkillDetail_Label5_Min_Damage_Taken;
                    break;
            }

            select1.SelectedValue = select1.Items[0];
            // 手动刷新 UI


            isSelect = false;
            // 暂停一次刷新
            _suspendUiUpdate = true;

            // 切换时清一次技能表，避免残留
            SkillTableDatas.SkillTable.Clear();

            // 立刻按新模式刷新一次
            bool isHeal = segmented1.SelectIndex != 0;
            SelectDataType();

            // 更新图表数据
            UpdateSkillDistributionChart();
            UpdateCritLuckyChart();

            // 下一轮计时器再恢复
            _suspendUiUpdate = false;

        }

        private void select1_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            if (isSelect) return;

            // 1) 确定指标（segmented1: 0=伤害 1=治疗 2=承伤）
            MetricType metric = segmented1.SelectIndex switch
            {
                1 => MetricType.Healing,
                2 => MetricType.Taken,
                _ => MetricType.Damage
            };

            // 2) 设置排序（统一返回 double，避免泛型不变性/类型不一致）
            SkillOrderBySelector = e.Value switch
            {
                0 => s => s.Total,       // 总量
                1 => s => s.TotalDps,    // 秒伤
                2 => s => s.HitCount,    // 次数
                3 => s => s.CritRate,    // 暴击率
                _ => s => s.Total
            };

            // 3) 确定数据源（单次/全程）
            SourceType source = FormManager.showTotal ? SourceType.FullRecord : SourceType.Current;

            // 4) 刷新技能表（内部会按 SkillOrderBySelector 排序）
            UpdateSkillTable(Uid, source, metric);

            // （可选）如果需要同时更新右侧图表：
            try { RefreshDpsTrendChart(); } catch { /* 忽略绘图异常 */ }
            UpdateSkillDistributionChart();
            UpdateCritLuckyChart();
        }

        public void GetPlayerInfo(string nickname, int power, string profession)
        {
            NickNameText.Text = nickname;
            PowerText.Text = power.ToString();
            UidText.Text = Uid.ToString();
            LevelLabel.Text = StatisticData._manager.GetAttrKV(Uid, "level")?.ToString() ?? "";
            Rank_levelLabel.Text = StatisticData._manager.GetAttrKV(Uid, "rank_level")?.ToString() ?? "";


            object? resourceObj = Properties.Resources.ResourceManager.GetObject(profession + "10");

            if (resourceObj is byte[] bytes)
            {
                using var ms = new MemoryStream(bytes);
                table_DpsDetailDataTable.BackgroundImage = Image.FromStream(ms);
            }
            else if (resourceObj is Image img)
            {
                table_DpsDetailDataTable.BackgroundImage = img;
            }
            else
            {
                table_DpsDetailDataTable.BackgroundImage = null; // 默认空白
            }

            if (_dpsTrendChart != null)
            {
                SetupTrendChartRefreshCallback();
                // 立即刷新图表数据
                RefreshDpsTrendChart();
            }
        }

        public void ResetDpsTrendChart()
        {
            if (_dpsTrendChart != null)
            {
                try
                {
                    _dpsTrendChart.FullReset();
                    ChartConfigManager.ApplySettings(_dpsTrendChart);

                    if (ChartVisualizationService.IsCapturing)
                    {
                        _dpsTrendChart.StartAutoRefresh(ChartConfigManager.REFRESH_INTERVAL);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"重置DPS趋势图表时出错: {ex.Message}");
                }
            }
        }

        private void splitter1_SplitterMoving(object sender, SplitterCancelEventArgs e)
        {
            if (e.SplitX < 350)
            {
                e.Cancel = true;
                return;
            }

            var offsetFrom350 = e.SplitX - 350;
            var steps = offsetFrom350 / SPLITTER_STEP_PIXELS; // 计算移动了多少个30px步长

            var newPadding = Math.Max(10, Math.Min(300, 160 - steps * PADDING_ADJUSTMENT));
            var newGridLines = Math.Max(3, Math.Min(10, 5 + steps));

            if (_dpsTrendChart != null)
            {
                var currentGridLines = _dpsTrendChart.GetVerticalGridLines();
                var currentPadding = _dpsTrendChart.GetPaddingRight();

                if (currentGridLines != newGridLines || currentPadding != newPadding)
                {
                    _dpsTrendChart.SetPaddingRight(newPadding);
                    _dpsTrendChart.SetVerticalGridLines(newGridLines);
                }
            }
        }

        private void splitter1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (_dpsTrendChart != null)
            {
                var offsetFrom350 = e.SplitX - 350;
                var steps = offsetFrom350 / SPLITTER_STEP_PIXELS;

                _lastSplitterPosition = 350 + steps * SPLITTER_STEP_PIXELS;

                var finalPadding = Math.Max(10, Math.Min(300, 160 - steps * PADDING_ADJUSTMENT));
                var finalGridLines = Math.Max(3, Math.Min(10, 5 + steps));

                _dpsTrendChart.SetPaddingRight(finalPadding);
                _dpsTrendChart.SetVerticalGridLines(finalGridLines);

                _dpsTrendChart.Invalidate();
            }
        }

        private void TitleText_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                FormManager.ReleaseCapture();
                FormManager.SendMessage(this.Handle, FormManager.WM_NCLBUTTONDOWN, FormManager.HTCAPTION, 0);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SelectDataType();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            _dpsTrendChart?.StopAutoRefresh();
            Close();
        }

        private void ApplyThemeToCharts()
        {
            var isDark = !Config.IsLight;
            if (_dpsTrendChart != null)
            {
                _dpsTrendChart.IsDarkTheme = isDark;
                _dpsTrendChart.Invalidate();
            }
            if (_skillDistributionChart != null)
            {
                _skillDistributionChart.IsDarkTheme = isDark;
                _skillDistributionChart.Invalidate();
            }
            if (_critLuckyChart != null)
            {
                _critLuckyChart.IsDarkTheme = isDark;
                _critLuckyChart.Invalidate();
            }
        }

        private void SkillDetailForm_ForeColorChanged(object sender, EventArgs e)
        {
            if (Config.IsLight)
            {
                table_DpsDetailDataTable.RowSelectedBg = ColorTranslator.FromHtml("#AED4FB");
                panel1.Back = panel2.Back = ColorTranslator.FromHtml("#67AEF6");
            }
            else
            {
                table_DpsDetailDataTable.RowSelectedBg = ColorTranslator.FromHtml("#10529a");
                panel1.Back = panel2.Back = ColorTranslator.FromHtml("#255AD0");
            }

            ApplyThemeToCharts();
        }

        private void InitializeSkillDistributionChart()
        {
            try
            {
                _skillDistributionChart = new FlatBarChart
                {
                    Dock = DockStyle.Fill,
                    TitleText = "",
                    XAxisLabel = "",
                    YAxisLabel = "",
                    IsDarkTheme = !Config.IsLight
                };
                collapseItem3.Controls.Clear();
                collapseItem3.Controls.Add(_skillDistributionChart);
                UpdateSkillDistributionChart();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"暴击率与幸运率图表初始化失败: {ex.Message}");
            }
        }

        private void InitializeCritLuckyChart()
        {
            try
            {
                _critLuckyChart = new FlatPieChart
                {
                    Dock = DockStyle.Fill,
                    TitleText = "",
                    ShowLabels = true,
                    ShowPercentages = true,
                    IsDarkTheme = !Config.IsLight
                };
                collapseItem2.Controls.Clear();
                collapseItem2.Controls.Add(_critLuckyChart);
                UpdateCritLuckyChart();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"技能占比图表初始化失败: {ex.Message}");
            }
        }
        public void ApplyLocalization()
        {
            AntdUI.SegmentedItem segmentedItem1 = new AntdUI.SegmentedItem();
            AntdUI.SegmentedItem segmentedItem2 = new AntdUI.SegmentedItem();
            AntdUI.SegmentedItem segmentedItem3 = new AntdUI.SegmentedItem();

            segmentedItem1.Text = Strings.Skill_Damage_Analysis;
            segmentedItem2.Text = Strings.Skill_Healing_Analysis;
            segmentedItem3.Text = Strings.Damage_Taken_Analysis;
            segmented1.Items.Clear();
            segmented1.Items.Add(segmentedItem1);
            segmented1.Items.Add(segmentedItem2);
            segmented1.Items.Add(segmentedItem3);

            label3.Text = Strings.SkillDetail_Label3;
            label2.Text = Strings.SkillDetail_Label2_DPS;
            label4.Text = Strings.SkillDetail_Label4_Damage_Crit;
            label5.Text = Strings.SkillDetail_Label5_Damage_Lucky;
            NumberCriticalHitsText.Text = Strings.SkillDetail_Label4_Damage_Crit;
            label19.Text = Strings.SkillDetail_Label19;

            collapseItem1.Text = Strings.SkillDetail_Dps_Hps_DTps_real_time_graph;
            collapseItem2.Text = Strings.SkillDetail_Skill_Distribution;
            collapseItem3.Text = Strings.SkillDetail_Damage_Distribution;

            collapse1.Items.Clear();
            collapse1.Items.Add(collapseItem1);
            collapse1.Items.Add(collapseItem2);
            collapse1.Items.Add(collapseItem3);

            PowerText.Prefix = Strings.SkillDetail_CombatPower;
            Rank_levelLabel.Prefix = Strings.SkillDetail_Armband;

            label17.Text = Strings.SkillDetail_Label17_Hits;

            label6.Text = Strings.SkillDetail_Label6_NormalDamage; //普通伤害
            label7.Text = Strings.SkillDetail_Label7_CritDamage; //暴击伤害
            label14.Text = Strings.SkillDetail_Label14_LuckyHits; //幸运次数
            label8.Text = Strings.SkillDetail_Label8_LuckyDamage; // 幸运伤害
            label9.Text = Strings.SkillDetail_Label9_AvgDamage; //平均伤害
            label13.Text = Strings.SkillDetail_Label13_HitsTaken; //挨打次数

            table_DpsDetailDataTable.Columns.Clear();

            table_DpsDetailDataTable.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("ID", "ID"), //Skill ID
                new AntdUI.Column("Name", Properties.Strings.SkillDetail_SkillName), //技能名
                new AntdUI.Column("Damage",Properties.Strings.SkillDetail_SkillDamage), //伤害
                new AntdUI.Column("TotalDps",Properties.Strings.SkillDetail_TotalDps), // DPS/秒
                new AntdUI.Column("HitCount",Properties.Strings.SkillDetail_HitCount), // 命中次数
                new AntdUI.Column("CritRate",Properties.Strings.SkillDetail_CritRate), // 暴击率
                new AntdUI.Column("AvgPerHit", Properties.Strings.SkillDetail_AvgPerHit), //平均伤害
                new AntdUI.Column("Percentage",Properties.Strings.SkillDetail_Percentage), //百分比
                new AntdUI.Column("HighestNonCrit", "HighestNonCrit"),
                new AntdUI.Column("HighestCrit", "HighestCrit"),
                new AntdUI.Column("Interval", "Interval")
            };
        }

        private void collapse1_ExpandChanged(object sender, CollapseExpandEventArgs e)
        {

        }

        private void LevelLabel_Click(object sender, EventArgs e)
        {

        }
    }
}
