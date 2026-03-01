using AntdUI;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Extends;
using StarResonanceDpsAnalysis.Forms;
using StarResonanceDpsAnalysis.Plugin;
using StarResonanceDpsAnalysis.Plugin.DamageStatistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using static StarResonanceDpsAnalysis.Forms.DpsStatisticsForm;

namespace StarResonanceDpsAnalysis.Control
{
    public partial class SkillDetailForm
    {
        #region 区块：上下文类型（决定数据来源）
        // 用于决定当前技能明细查看的是：当前战斗 / 全程累计 / 历史快照
        public enum DetailContextType
        {
            Current,     // 当前战斗（默认）
            FullRecord,  // 全程（累计值）
            Snapshot     // 历史快照（按某一战斗片段的固化数据）
        }
        #endregion

        #region 区块：列表表头配置（仅负责表格列定义与绑定）
        public void ToggleTableView()
        {
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

            // 绑定数据源：SkillTableDatas.SkillTable（外部维护的数据集合）
            table_DpsDetailDataTable.Binding(SkillTableDatas.SkillTable);
        }
        #endregion

        #region 区块：实例级字段（由外层窗体/调用方注入）
        public ulong Uid;           // 当前查看的玩家 UID
        public string Nickname;     // 玩家昵称（展示用）
        public int Power;           // 玩家战力（展示用）
        public string Profession;   // 玩家职业（展示用）

        // 技能排序选择器：默认按 Total 倒序；可通过外部替换该委托实现“改排序规则不改代码”
        public Func<SkillSummary, double> SkillOrderBySelector = s => s.Total;
        #endregion

        #region 私有小工具（为了替换 Linq，全部用最朴素写法）
        private SkillData FindRowBySkillId(ulong skillId)
        {
            for (int i = 0; i < SkillTableDatas.SkillTable.Count; i++)
            {
                if (SkillTableDatas.SkillTable[i].SkillId == skillId)
                    return SkillTableDatas.SkillTable[i];
            }
            return null;
        }

        private double SumTotal(List<SkillSummary> list)
        {
            double sum = 0;
            for (int i = 0; i < list.Count; i++)
            {
                sum += list[i].Total;
            }
            return sum;
        }

        private void SortSkillsDesc(List<SkillSummary> list)
        {
            // 按 SkillOrderBySelector 降序
            list.Sort(delegate (SkillSummary a, SkillSummary b)
            {
                double va = SkillOrderBySelector != null ? SkillOrderBySelector(a) : a.Total;
                double vb = SkillOrderBySelector != null ? SkillOrderBySelector(b) : b.Total;
                if (va < vb) return 1;   // 降序
                if (va > vb) return -1;
                return 0;
            });
        }

        private List<SkillSummary> ToListOrEmpty(IReadOnlyList<SkillSummary> src)
        {
            var list = new List<SkillSummary>();
            if (src == null) return list;
            for (int i = 0; i < src.Count; i++) list.Add(src[i]);
            return list;
        }
        #endregion

        #region 区块：表格数据刷新（单次/全程入口）
        /// <summary>
        /// 刷新并填充玩家技能表格
        /// 根据 source（Current/FullRecord）与 metric（Damage/Healing/Taken）决定取数口径。
        /// 统一把返回的技能清单映射为 SkillData 并写入 SkillTableDatas.SkillTable。
        /// </summary>
        public void UpdateSkillTable(ulong uid, SourceType source, MetricType metric)
        {
            // SkillTableDatas.SkillTable.Clear();

            // 取技能清单（统一成同样的结构）
            List<SkillSummary> skills;
            if (source == SourceType.Current)
            {
                if (metric == MetricType.Taken)
                {
                    var temp = StatisticData._manager.GetPlayerTakenDamageSummaries(uid, null, true);
                    skills = ToListOrEmpty(temp);
                    SortSkillsDesc(skills);
                }
                else
                {
                    var skillType = (metric == MetricType.Healing)
                        ? StarResonanceDpsAnalysis.Core.SkillType.Heal
                        : StarResonanceDpsAnalysis.Core.SkillType.Damage;

                    var temp = StatisticData._manager.GetPlayerSkillSummaries(uid, null, true, skillType); // TODO Here translate skills | completed
                    skills = ToListOrEmpty(temp);
                    SortSkillsDesc(skills);
                }
            }
            else
            {
                // 全程累计：从 FullRecord 读取（damage/heal/taken 三类分开取）
                var triple = FullRecord.GetPlayerSkills(uid);
                if (metric == MetricType.Healing)
                {
                    skills = ToListOrEmpty(triple.Item2);
                    SortSkillsDesc(skills);
                }
                else if (metric == MetricType.Taken)
                {
                    skills = ToListOrEmpty(triple.Item3);
                    SortSkillsDesc(skills);
                }
                else
                {
                    skills = ToListOrEmpty(triple.Item1);
                    SortSkillsDesc(skills);
                }
            }

            // 计算各技能在本次清单中的占比（ShareOfTotal）
            double grandTotal = SumTotal(skills);

            for (int i = 0; i < skills.Count; i++)
            {
                var item = skills[i];
                double share = grandTotal > 0 ? (double)item.Total / grandTotal : 0.0;

                string critRateStr = item.CritRate.ToString() + "%";
                string luckyRateStr = item.LuckyRate.ToString() + "%";

                // 查找是否已有相同行
                var existing = FindRowBySkillId(item.SkillId);

                if (existing == null)
                {
                    var newRow = new SkillData(
                        item.SkillId,
                        // item.SkillName,
                        EmbeddedSkillConfig.GetLocalizedSkillDefinition(item.SkillId.ToString()).Name,
                        null,
                        item.Total,
                        item.HitCount,
                        critRateStr,
                        share,
                        item.AvgPerHit,
                        item.TotalDps,
                        item.HighestNonCrit,
                        item.HighestCrit,
                        item.Interval
                    );

                    newRow.Share = new CellProgress((float)share);
                    newRow.Share.Fill = AppConfig.DpsColor;
                    newRow.Share.Size = new Size(200, 10);
                    
                    SkillTableDatas.SkillTable.Add(newRow);
                }
                else
                {
                    existing.SkillId = item.SkillId;
                    // existing.Name = item.SkillName;
                    existing.Name = EmbeddedSkillConfig.GetLocalizedSkillDefinition(item.SkillId.ToString()).Name;
                    existing.Damage = new CellText(item.Total.ToString()) { Font = AppConfig.ContentFont };
                    existing.HitCount = new CellText(item.HitCount.ToString()) { Font = AppConfig.ContentFont };
                    existing.CritRate = new CellText(critRateStr) { Font = AppConfig.ContentFont };
                    existing.AvgPerHit = new CellText(item.AvgPerHit.ToString()) { Font = AppConfig.ContentFont };
                    existing.TotalDps = new CellText(item.TotalDps.ToString()) { Font = AppConfig.ContentFont };
                    existing.Percentage = new CellText(share.ToString()) { Font = AppConfig.ContentFont };
                    existing.HighestNonCrit = new CellText(item.HighestNonCrit.ToString()) { Font = AppConfig.DigitalFont };
                    existing.HighestCrit = new CellText(item.HighestCrit.ToString()) { Font = AppConfig.DigitalFont };
                    existing.Interval = new CellText(item.Interval.ToString("F2")) { Font = AppConfig.DigitalFont };

                    var cp = new CellProgress((float)share);
                    cp.Fill = AppConfig.DpsColor;
                    cp.Size = new Size(200, 10);
                    existing.Share = cp;
                }
            }
        }
        #endregion

        #region 区块：数据类型切换（入口调度：顶部汇总 + 表格 + 图表）
        /// <summary>
        /// 根据 UI 状态（ContextType、segmented1）选择数据类型并刷新：
        /// - 快照：直接用快照数据渲染顶部与表格（不拉实时曲线）
        /// - 非快照：根据 showTotal / ContextType 选择 Current 或 FullRecord
        /// </summary>
        public void SelectDataType()
        {
            // 1) 根据 segmented1 决定当前查看指标（伤害/治疗/承伤）
            MetricType metric;
            if (segmented1.SelectIndex == 1) metric = MetricType.Healing;
            else if (segmented1.SelectIndex == 2)
            {
                metric = MetricType.Taken;
            }
            else metric = MetricType.Damage;

            // 2) 快照模式：不刷新实时曲线，只渲染顶部与表格、静态图
            if (ContextType == DetailContextType.Snapshot && SnapshotStartTime is DateTime)
            {
                DateTime snapTime = (DateTime)SnapshotStartTime;
                try
                {
                    FillHeader(metric);                    // 顶部汇总（快照口径）
                    UpdateSkillTable_Snapshot(Uid, snapTime, metric); // 表格（快照口径）
                }
                catch { }

                try { UpdateCritLuckyChart(); } catch { }
                try { UpdateSkillDistributionChart(); } catch { }
                return;
            }

            // 3) 非快照：按 Current / FullRecord 渲染
            SourceType source;
            if (ContextType == DetailContextType.FullRecord || FormManager.showTotal) source = SourceType.FullRecord;
            else source = SourceType.Current;

            FillHeader(metric); // 顶部汇总（Current/FullRecord）

            try
            {
                UpdateSkillTable(Uid, source, metric);   // 列表
                RefreshDpsTrendChart();                  // 趋势图（仅非快照）
                UpdateCritLuckyChart();                  // 技能占比图
                UpdateSkillDistributionChart();          // 普通/暴击/幸运分布图
            }
            catch { }
        }
        #endregion

        #region 区块：顶部汇总渲染（快照 / 当前 / 全程）
        private void FillHeader(MetricType metric)
        {
            // ======== 快照模式（历史）========
            if (ContextType == DetailContextType.Snapshot && SnapshotStartTime is DateTime)
            {
                DateTime snapTime = (DateTime)SnapshotStartTime;

                var sessionDict = FullRecord.GetAllPlayersDataBySnapshotTime(snapTime);

                SnapshotPlayer sp = null;
                if (sessionDict != null)
                {
                    SnapshotPlayer tmp;
                    if (sessionDict.TryGetValue(Uid, out tmp)) sp = tmp;
                }

                if (sp == null)
                {
                    // 在 _manager.History 中寻找开始时间相等或 ±2 秒内的战斗
                    BattleSnapshot battle = null;
                    var history = StatisticData._manager.History;
                    if (history != null)
                    {
                        for (int i = 0; i < history.Count; i++)
                        {
                            var s = history[i];
                            double delta = Math.Abs((s.StartedAt - snapTime).TotalSeconds);
                            if (s.StartedAt == snapTime || delta <= 2.0)
                            {
                                battle = s;
                                break;
                            }
                        }
                    }
                    if (battle != null && battle.Players != null)
                    {
                        SnapshotPlayer tmp2;
                        if (battle.Players.TryGetValue(Uid, out tmp2)) sp = tmp2;
                    }
                }

                if (sp == null)
                {
                    TotalDamageText.Text = "0";
                    TotalDpsText.Text = "0";
                    CritRateText.Text = "0";
                    LuckyRate.Text = "0";
                    NormalDamageText.Text = "0";
                    CritDamageText.Text = "0";
                    LuckyDamageText.Text = "0";
                    AvgDamageText.Text = "0";
                    NumberHitsLabel.Text = "0";
                    NumberCriticalHitsLabel.Text = "0";
                    LuckyTimesLabel.Text = "0";
                    BeatenLabel.Text = "0";
                    return;
                }

                // —— 工具函数（快照模式内部估算用）
                static int SumHits(IReadOnlyList<SkillSummary> list)
                {
                    if (list == null) return 0;
                    int total = 0;
                    for (int i = 0; i < list.Count; i++) total += list[i].HitCount;
                    return total;
                }

                static int EstCrits(IReadOnlyList<SkillSummary> list)
                {
                    if (list == null) return 0;
                    double total = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var s = list[i];
                        total += s.HitCount * (s.CritRate / 100.0);
                    }
                    return (int)Math.Round(total);
                }

                static int EstLuckies(IReadOnlyList<SkillSummary> list)
                {
                    if (list == null) return 0;
                    double total = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var s = list[i];
                        total += s.HitCount * (s.LuckyRate / 100.0);
                    }
                    return (int)Math.Round(total);
                }

                static double WeightedRate(IReadOnlyList<SkillSummary> list, Func<SkillSummary, double> selector)
                {
                    if (list == null || list.Count == 0) return 0;
                    int hits = SumHits(list);
                    if (hits <= 0) return 0;

                    double weightedSum = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var s = list[i];
                        weightedSum += s.HitCount * selector(s);
                    }
                    return Math.Round(weightedSum / hits, 2);
                }

                static ulong SafeUlong(double v)
                {
                    if (v <= 0) return 0UL;
                    return (ulong)Math.Round(v);
                }

                var dmgSkills = sp.DamageSkills;
                var healSkills = sp.HealingSkills;
                var takenSkills = sp.TakenSkills;

                if (metric == MetricType.Damage)
                {
                    var total = sp.TotalDamage;
                    var dps = sp.TotalDps;
                    int hits = SumHits(dmgSkills);
                    double avg = (hits > 0) ? ((double)total / hits) : 0.0;

                    long normal = (long)total - (long)sp.CriticalDamage - (long)sp.LuckyDamage + (long)sp.CritLuckyDamage;
                    if (normal < 0) normal = 0;

                    double critRate = (sp.CritRate > 0) ? sp.CritRate : WeightedRate(dmgSkills, delegate (SkillSummary s) { return s.CritRate; });
                    double luckyRate = (sp.LuckyRate > 0) ? sp.LuckyRate : WeightedRate(dmgSkills, delegate (SkillSummary s) { return s.LuckyRate; });

                    TotalDamageText.Text = Common.FormatWithEnglishUnits(total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(dps);
                    CritRateText.Text = critRate.ToString() + "%";
                    LuckyRate.Text = luckyRate.ToString() + "%";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(SafeUlong(normal));
                    CritDamageText.Text = Common.FormatWithEnglishUnits(sp.CriticalDamage);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(sp.LuckyDamage);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(avg);

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(hits);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(EstCrits(dmgSkills));
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(EstLuckies(dmgSkills));
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(SumHits(takenSkills));
                }
                else if (metric == MetricType.Healing)
                {
                    var total = sp.TotalHealing;
                    var hps = sp.TotalHps;
                    int hits = SumHits(healSkills);
                    double avg = (hits > 0) ? ((double)total / hits) : 0.0;

                    long normalHeal = (long)total - (long)sp.HealingCritical - (long)sp.HealingLucky + (long)sp.HealingCritLucky;
                    if (normalHeal < 0) normalHeal = 0;

                    double critRate = WeightedRate(healSkills, delegate (SkillSummary s) { return s.CritRate; });
                    double luckyRate = WeightedRate(healSkills, delegate (SkillSummary s) { return s.LuckyRate; });

                    TotalDamageText.Text = Common.FormatWithEnglishUnits(total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(hps);
                    CritRateText.Text = critRate.ToString() + "%";
                    LuckyRate.Text = luckyRate.ToString() + "%";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(SafeUlong(normalHeal));
                    CritDamageText.Text = Common.FormatWithEnglishUnits(sp.HealingCritical);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(sp.HealingLucky);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(avg);

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(hits);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(EstCrits(healSkills));
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(EstLuckies(healSkills));
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(hits);
                }
                else // Taken
                {
                    ulong total = sp.TakenDamage;
                    int hits = SumHits(takenSkills);
                    double perSecond = 0.0; // 如需可用快照时长计算

                    // 最大/最小承伤（>0 的最小）
                    ulong maxSingle = 0UL;
                    ulong minSingle = 0UL;
                    if (takenSkills != null && takenSkills.Count > 0)
                    {
                        // max
                        for (int i = 0; i < takenSkills.Count; i++)
                        {
                            if (takenSkills[i].MaxSingleHit > maxSingle)
                                maxSingle = takenSkills[i].MaxSingleHit;
                        }
                        // min (>0)
                        bool hasMin = false;
                        for (int i = 0; i < takenSkills.Count; i++)
                        {
                            ulong v = takenSkills[i].MinSingleHit;
                            if (v > 0)
                            {
                                if (!hasMin)
                                {
                                    minSingle = v;
                                    hasMin = true;
                                }
                                else
                                {
                                    if (v < minSingle) minSingle = v;
                                }
                            }
                        }
                        if (!hasMin) minSingle = 0;
                    }

                    TotalDamageText.Text = Common.FormatWithEnglishUnits(total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(perSecond);
                    CritRateText.Text = Common.FormatWithEnglishUnits(maxSingle);   // UI 约定：最大承伤
                    CritDamageText.Text = Common.FormatWithEnglishUnits(minSingle); // UI 约定：最小承伤
                    LuckyRate.Text = "0";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(total);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(0);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(0);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(hits > 0 ? (double)total / hits : 0.0);

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(hits);
                    NumberCriticalHitsLabel.Text = "0";
                    LuckyTimesLabel.Text = "0";
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(hits);
                }

                return; // 快照分支结束
            }

            // ======== 非快照（单次 / 全程）========
            SourceType src;
            if (ContextType == DetailContextType.FullRecord || FormManager.showTotal) src = SourceType.FullRecord;
            else src = SourceType.Current;

            if (src == SourceType.Current)
            {
                var p = StatisticData._manager.GetOrCreate(Uid);

                if (metric == MetricType.Damage)
                {
                    TotalDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.Total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(p.GetTotalDps());
                    CritRateText.Text = p.DamageStats.GetCritRate().ToString() + "%";
                    LuckyRate.Text = p.DamageStats.GetLuckyRate().ToString() + "%";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.Normal);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.Critical);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.LuckyAndCritical);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.GetAveragePerHit());

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(p.DamageStats.CountTotal);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(p.DamageStats.CountCritical);
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(p.DamageStats.CountLucky);
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountTotal);
                }
                else if (metric == MetricType.Healing)
                {
                    TotalDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.Total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(p.GetTotalHps());
                    CritRateText.Text = p.HealingStats.GetCritRate().ToString() + "%";
                    LuckyRate.Text = p.HealingStats.GetLuckyRate().ToString() + "%";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.Normal);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.Critical);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.LuckyAndCritical);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.GetAveragePerHit());

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountTotal);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountCritical);
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountLucky);
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountTotal);
                }
                else // Taken
                {
                    var taken = StatisticData._manager.GetPlayerTakenOverview(Uid);
                    TotalDamageText.Text = Common.FormatWithEnglishUnits(taken.Total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(taken.AvgTakenPerSec);
                    CritRateText.Text = Common.FormatWithEnglishUnits(taken.MaxSingleHit); // 最大承伤
                    CritDamageText.Text = Common.FormatWithEnglishUnits(taken.MinSingleHit); // 最小承伤
                    LuckyRate.Text = "0";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.Total);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.Critical);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.LuckyAndCritical);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.GetAveragePerHit());

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountTotal);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountCritical);
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountLucky);
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountTotal);
                }
            }
            else // FullRecord
            {
                var p = FullRecord.Shim.GetOrCreate(Uid);

                if (metric == MetricType.Damage)
                {
                    TotalDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.Total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(p.GetTotalDps());
                    CritRateText.Text = p.DamageStats.GetCritRate().ToString() + "%";
                    LuckyRate.Text = p.DamageStats.GetLuckyRate().ToString() + "%";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.Normal);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.Critical);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.Lucky);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(p.DamageStats.GetAveragePerHit());

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(p.DamageStats.CountTotal);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(p.DamageStats.CountCritical);
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(p.DamageStats.CountLucky);
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountTotal);
                }
                else if (metric == MetricType.Healing)
                {
                    TotalDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.Total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(p.GetTotalHps());
                    CritRateText.Text = p.HealingStats.GetCritRate().ToString() + "%";
                    LuckyRate.Text = p.HealingStats.GetLuckyRate().ToString() + "%";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.Normal);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.Critical);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.Lucky);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(p.HealingStats.GetAveragePerHit());

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountTotal);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountCritical);
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountLucky);
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(p.HealingStats.CountTotal);
                }
                else // Taken
                {
                    var taken = FullRecord.Shim.GetPlayerTakenOverview(Uid);
                    TotalDamageText.Text = Common.FormatWithEnglishUnits(taken.Total);
                    TotalDpsText.Text = Common.FormatWithEnglishUnits(taken.AvgTakenPerSec);
                    CritRateText.Text = Common.FormatWithEnglishUnits(taken.MaxSingleHit);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(taken.MinSingleHit);
                    LuckyRate.Text = "0";

                    NormalDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.Total);
                    CritDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.Critical);
                    LuckyDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.Lucky);
                    AvgDamageText.Text = Common.FormatWithEnglishUnits(p.TakenStats.GetAveragePerHit());

                    NumberHitsLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountTotal);
                    NumberCriticalHitsLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountCritical);
                    LuckyTimesLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountLucky);
                    BeatenLabel.Text = Common.FormatWithEnglishUnits(p.TakenStats.CountTotal);
                }
            }
        }
        #endregion

        #region 区块：图表—普通/暴击/幸运分布（柱状）
        /// <summary>
        /// 更新普通/暴击/幸运比例的柱状分布图。
        /// 数据来源：Current 或 FullRecord 的 Damage/Healing/Taken 统计对象。
        /// </summary>
        private void UpdateSkillDistributionChart()
        {
            if (_skillDistributionChart == null) return;

            try
            {
                SourceType source;
                if (ContextType == DetailContextType.FullRecord || FormManager.showTotal) source = SourceType.FullRecord;
                else source = SourceType.Current;

                MetricType metric;
                if (segmented1.SelectIndex == 1) metric = MetricType.Healing;
                else if (segmented1.SelectIndex == 2) metric = MetricType.Taken;
                else metric = MetricType.Damage;

                double critRate = 0;
                double luckyRate = 0;
                if (source == SourceType.Current)
                {
                    var p = StatisticData._manager.GetOrCreate(Uid);
                    if (metric == MetricType.Healing)
                    {
                        critRate = p.HealingStats.GetCritRate();
                        luckyRate = p.HealingStats.GetLuckyRate();
                    }
                    else if (metric == MetricType.Taken)
                    {
                        critRate = p.TakenStats.GetCritRate();
                        luckyRate = p.TakenStats.GetLuckyRate();
                    }
                    else
                    {
                        critRate = p.DamageStats.GetCritRate();
                        luckyRate = p.DamageStats.GetLuckyRate();
                    }
                }
                else
                {
                    var p = FullRecord.Shim.GetOrCreate(Uid);
                    if (metric == MetricType.Healing)
                    {
                        critRate = p.HealingStats.GetCritRate();
                        luckyRate = p.HealingStats.GetLuckyRate();
                    }
                    else if (metric == MetricType.Taken)
                    {
                        critRate = p.TakenStats.GetCritRate();
                        luckyRate = p.TakenStats.GetLuckyRate();
                    }
                    else
                    {
                        critRate = p.DamageStats.GetCritRate();
                        luckyRate = p.DamageStats.GetLuckyRate();
                    }
                }

                double normalRate = 100 - critRate - luckyRate;
                if (normalRate < 0) normalRate = 0;

                var chartData = new List<(string, double)>();
                if (normalRate > 0) chartData.Add(("Normal", normalRate)); // 普通
                if (critRate > 0) chartData.Add(("Critical", critRate)); // 暴击
                if (luckyRate > 0) chartData.Add(("Lucky", luckyRate)); // 幸运

                _skillDistributionChart.SetData(chartData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("更新暴击率与幸运率图表时出错: " + ex.Message);
            }
        }
        #endregion

        #region 区块：图表—技能占比（饼图/环图）
        /// <summary>
        /// 更新技能占比图（取 Top10 技能，总量作为扇区值）。
        /// 数据来源：Current 或 FullRecord；指标与 segmented1 一致。
        /// </summary>
        private void UpdateCritLuckyChart()
        {
            if (_critLuckyChart == null) return;

            try
            {
                SourceType source;
                if (ContextType == DetailContextType.FullRecord || FormManager.showTotal) source = SourceType.FullRecord;
                else source = SourceType.Current;

                MetricType metric;
                if (segmented1.SelectIndex == 1) metric = MetricType.Healing;
                else if (segmented1.SelectIndex == 2) metric = MetricType.Taken;
                else metric = MetricType.Damage;

                List<SkillSummary> skills = new List<SkillSummary>();

                if (source == SourceType.Current)
                {
                    if (metric == MetricType.Healing)
                    {
                        var tmp = StatisticData._manager.GetPlayerSkillSummaries(Uid, 10, true, StarResonanceDpsAnalysis.Core.SkillType.Heal);
                        skills = ToListOrEmpty(tmp);
                    }
                    else if (metric == MetricType.Taken)
                    {
                        var tmp = StatisticData._manager.GetPlayerTakenDamageSummaries(Uid, 10, true);
                        skills = ToListOrEmpty(tmp);
                    }
                    else
                    {
                        var tmp = StatisticData._manager.GetPlayerSkillSummaries(Uid, 10, true, StarResonanceDpsAnalysis.Core.SkillType.Damage);
                        skills = ToListOrEmpty(tmp);
                    }
                    // 保险：再按排序器排序一遍，截前10
                    SortSkillsDesc(skills);
                    if (skills.Count > 10) skills = skills.GetRange(0, 10);
                }
                else
                {
                    var triple = FullRecord.GetPlayerSkills(Uid);
                    if (metric == MetricType.Healing)
                        skills = ToListOrEmpty(triple.Item2);
                    else if (metric == MetricType.Taken)
                        skills = ToListOrEmpty(triple.Item3);
                    else
                        skills = ToListOrEmpty(triple.Item1);

                    SortSkillsDesc(skills);
                    if (skills.Count > 10) skills = skills.GetRange(0, 10);
                }

                var chartData = new List<(string, double)>();
                for (int i = 0; i < skills.Count; i++)
                {
                    string enName = EmbeddedSkillConfig.GetLocalizedSkillDefinition(skills[i].SkillId.ToString()).Name;
                    chartData.Add((enName, (double)skills[i].Total));
                }
                _critLuckyChart.SetData(chartData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("更新技能占比图时出错: " + ex.Message);
            }
        }
        #endregion

        #region 区块：快照表格填充（独立数据口径）
        /// <summary>
        /// 基于快照时间与 UID，填充技能表格：
        /// - 通过 FullRecord.GetPlayerSkillsBySnapshotTimeEx 获取三类技能列表
        /// - 仅渲染表格，不拉实时曲线
        /// </summary>
        private void UpdateSkillTable_Snapshot(ulong uid, DateTime startedAt, MetricType metric)
        {
            SkillTableDatas.SkillTable.Clear();

            var triple = FullRecord.GetPlayerSkillsBySnapshotTimeEx(startedAt, uid);

            List<SkillSummary> skills = new List<SkillSummary>();
            if (metric == MetricType.Healing)
                skills = ToListOrEmpty(triple.Item2);
            else if (metric == MetricType.Taken)
                skills = ToListOrEmpty(triple.Item3);
            else
                skills = ToListOrEmpty(triple.Item1);

            SortSkillsDesc(skills);

            // 计算占比并生成行
            double grandTotal = 0;
            for (int i = 0; i < skills.Count; i++) grandTotal += skills[i].Total;

            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                double share = grandTotal > 0 ? (double)s.Total / grandTotal : 0.0;

                var row = new SkillData(
                    s.SkillId,
                    EmbeddedSkillConfig.GetLocalizedSkillDefinition(s.SkillId.ToString()).Name,
                //s.SkillName,
                    null,
                    s.Total,
                    s.HitCount,
                    s.CritRate.ToString() + "%",
                    share,
                    s.AvgPerHit,
                    s.TotalDps,
                    s.HighestNonCrit,
                    s.HighestCrit,
                    s.Interval
                );

                var cp = new CellProgress((float)share);
                cp.Size = new Size(200, 10);
                row.Share = cp;

                SkillTableDatas.SkillTable.Add(row);
            }

            // 如需排查快照无数据问题，可临时打开以下弹窗确认数据来源是否为空
            // if (SkillTableDatas.SkillTable.Count == 0)
            // {
            //     MessageBox.Show("快照技能为空：请检查 GetPlayerSkillsBySnapshotTimeEx 是否返回了空列表。");
            // }
        }
        #endregion
    }
}
