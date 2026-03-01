using AntdUI;
using BlueProto;
using System.ComponentModel;

namespace StarResonanceDpsAnalysis.Plugin
{
    #region DpsTableDatas 类
    public class DpsTableDatas
    {
        /// <summary>
        /// 表格数据绑定
        /// </summary>
        public static BindingList<DpsTable> DpsTable = [];
        public static readonly object DpsTableLock = new();

    }
    #endregion
    #region DpsTable 类
    /// <summary>
    /// DPS 表格数据模型
    /// 用于绑定 UI 表格显示单个玩家的战斗统计信息（伤害、治疗、承伤等）
    /// 继承 NotifyProperty 以支持属性更改通知（UI 自动刷新）
    /// </summary>
    public class DpsTable : NotifyProperty
    {
        // —— DPS 相关私有字段（只在类内部使用） ——
        private ulong uid;                // 玩家唯一ID
        private string nickName;           // 玩家昵称
        private string profession;         // 职业
        private int combatPower;           // 战力
        private string totalDamage;        // 总伤害
        private string criticalDamage;     // 暴击伤害
        private string luckyDamage;        // 幸运伤害
        private string critLuckyDamage;    // 暴击且幸运的伤害
        private string critRate;           // 暴击率（百分比字符串）
        private string luckyRate;          // 幸运率（百分比字符串）
        private string instantDps;         // 实时 DPS
        private string maxInstantDps;      // 最大瞬时 DPS
        private string totalDps;           // 平均 DPS
        private double dmgShare; // 用于 UI 显示的伤害占比进度条

        // —— HPS 相关私有字段（治疗类数据） ——
        private string damageTaken;        // 承受伤害总量
        private string totalHealingDone;   // 总治疗量
        private string criticalHealingDone;// 暴击治疗量
        private string luckyHealingDone;   // 幸运治疗量
        private string critLuckyHealingDone;// 暴击且幸运的治疗量
        private string instantHps;         // 实时 HPS
        private string maxInstantHps;      // 最大瞬时 HPS
        private string totalHps;           // 平均 HPS


        /// <summary>
        /// 构造函数
        /// 初始化所有统计字段的值（UI 初次绑定时使用）
        /// </summary>
        public DpsTable(
            ulong uid,
            string nickname,
            ulong takenDamage,
            ulong totalHealing,
            ulong totalCriticalHealing,
            ulong totalLuckyHealing,
            ulong totalCritLuckyHealing,
            ulong totalInstantHps,
            ulong totalMaxInstantHps,
            string profession,
            ulong totalDamage,
            ulong criticalDamage,
            ulong luckyDamage,
            ulong critLuckyDamage,
            double critRate,
            double luckyRate,
            ulong instantDps,
            ulong maxInstantDps,
            double totalDps,
            double totalHps,
            int combatPower = 0, double dmgShare = 0)
        {
            // —— 基础信息 ——
            Uid = uid;
            NickName = nickname;
            CombatPower = combatPower;
            Profession = profession;

            // —— 伤害相关 ——
            TotalDamage = totalDamage.ToString();
            CriticalDamage = criticalDamage.ToString();
            LuckyDamage = luckyDamage.ToString();
            CritLuckyDamage = critLuckyDamage.ToString();
            CritRate = $"{critRate}%";
            LuckyRate = $"{luckyRate}%";
            InstantDps = instantDps.ToString();
            MaxInstantDps = maxInstantDps.ToString();
            TotalDps = totalDps.ToString();

            // —— 承伤/治疗相关 ——
            DamageTaken = takenDamage.ToString();
            TotalHealingDone = totalHealing.ToString();
            CriticalHealingDone = totalCriticalHealing.ToString();
            LuckyHealingDone = totalLuckyHealing.ToString();
            CritLuckyHealingDone = totalCritLuckyHealing.ToString();
            InstantHps = totalInstantHps.ToString();
            MaxInstantHps = totalMaxInstantHps.ToString();
            TotalHps = totalHps.ToString();

            // —— UI 占比进度条 ——
            DmgShare = dmgShare;
        }

        // —— 属性封装（支持 UI 绑定通知） ——

        /// <summary>玩家唯一ID</summary>
        public ulong Uid
        {
            get => uid;
            set
            {

                if (uid == value) return;
                uid = value;
                OnPropertyChanged(nameof(Uid));
            }
        }

        /// <summary>玩家昵称</summary>
        public string NickName
        {
            get => nickName;
            set
            {

                if (nickName == value) return;
                nickName = value;
                OnPropertyChanged(nameof(NickName));
            }
        }



        /// <summary>战力</summary>
        public int CombatPower
        {
            get => combatPower;
            set
            {
                if (combatPower == value) return;
                combatPower = value;
                OnPropertyChanged(nameof(CombatPower));
            }
        }

        /// <summary>职业</summary>
        public string Profession
        {
            get => profession;
            set
            {
                if (profession == value) return;
                profession = value;
                OnPropertyChanged(nameof(Profession));
            }
        }


        /// <summary>
        /// 暴击率
        /// </summary>
        public string CritRate
        {
            get => critRate;
            set
            {
                if (critRate == value) return;
                critRate = value;
                OnPropertyChanged(nameof(CritRate));
            }
        }

        public string LuckyRate
        {
            get => luckyRate;
            set
            {
                if (luckyRate == value) return;
                luckyRate = value;
                OnPropertyChanged(nameof(LuckyRate));
            }
        }

        /// <summary>总伤害</summary>
        public string TotalDamage
        {
            get => totalDamage;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (totalDamage == formatted) return;
                totalDamage = formatted;
                OnPropertyChanged(nameof(TotalDamage));
            }
        }

        /// <summary>暴击伤害</summary>
        public string CriticalDamage
        {
            get => criticalDamage;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (criticalDamage == formatted) return;
                criticalDamage = formatted;
                OnPropertyChanged(nameof(CriticalDamage));
            }
        }

        /// <summary>幸运伤害</summary>
        public string LuckyDamage
        {
            get => luckyDamage;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (luckyDamage == formatted) return;
                luckyDamage = formatted;
                OnPropertyChanged(nameof(LuckyDamage));
            }
        }

        /// <summary>暴击且幸运伤害</summary>
        public string CritLuckyDamage
        {
            get => critLuckyDamage;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (critLuckyDamage == formatted) return;
                critLuckyDamage = formatted;
                OnPropertyChanged(nameof(CritLuckyDamage));
            }
        }

        /// <summary>实时 DPS</summary>
        public string InstantDps
        {
            get => instantDps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (instantDps == formatted) return;
                instantDps = formatted;
                OnPropertyChanged(nameof(InstantDps));
            }
        }

        /// <summary>最大瞬时 DPS</summary>
        public string MaxInstantDps
        {
            get => maxInstantDps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (maxInstantDps == formatted) return;
                maxInstantDps = formatted;
                OnPropertyChanged(nameof(MaxInstantDps));
            }
        }

        /// <summary>平均 DPS</summary>
        public string TotalDps
        {
            get => totalDps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);

                if (totalDps == formatted) return;
                totalDps = formatted;
                OnPropertyChanged(nameof(TotalDps));
            }
        }

        /// <summary>承受伤害</summary>
        public string DamageTaken
        {
            get => damageTaken;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (damageTaken == formatted) return;
                damageTaken = formatted;
                OnPropertyChanged(nameof(DamageTaken));
            }
        }

        /// <summary>总治疗量</summary>
        public string TotalHealingDone
        {
            get => totalHealingDone;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (totalHealingDone == formatted) return;
                totalHealingDone = formatted;
                OnPropertyChanged(nameof(TotalHealingDone));
            }
        }

        /// <summary>暴击治疗量</summary>
        public string CriticalHealingDone
        {
            get => criticalHealingDone;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (criticalHealingDone == formatted) return;
                criticalHealingDone = formatted;
                OnPropertyChanged(nameof(CriticalHealingDone));
            }
        }

        /// <summary>幸运治疗量</summary>
        public string LuckyHealingDone
        {
            get => luckyHealingDone;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (luckyHealingDone == formatted) return;
                luckyHealingDone = formatted;
                OnPropertyChanged(nameof(LuckyHealingDone));
            }
        }

        /// <summary>暴击且幸运的治疗量</summary>
        public string CritLuckyHealingDone
        {
            get => critLuckyHealingDone;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (critLuckyHealingDone == formatted) return;
                critLuckyHealingDone = formatted;
                OnPropertyChanged(nameof(CritLuckyHealingDone));
            }
        }

        /// <summary>实时 HPS</summary>
        public string InstantHps
        {
            get => instantHps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (instantHps == formatted) return;
                instantHps = formatted;
                OnPropertyChanged(nameof(InstantHps));
            }
        }

        /// <summary>最大瞬时 HPS</summary>
        public string MaxInstantHps
        {
            get => maxInstantHps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (maxInstantHps == formatted) return;
                maxInstantHps = formatted;
                OnPropertyChanged(nameof(MaxInstantHps));
            }
        }

        /// <summary>平均 HPS</summary>
        public string TotalHps
        {
            get => totalHps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value));
                string formatted = Common.FormatWithEnglishUnits(val);
                if (totalHps == formatted) return;
                totalHps = formatted;
                OnPropertyChanged(nameof(TotalHps));
            }
        }


        /// <summary>伤害占比进度条</summary>
        public double DmgShare
        {
            get => dmgShare;
            set
            {
                if (dmgShare == value) return;
                dmgShare = value;
                OnPropertyChanged(nameof(DmgShare));
            }
        }
    }


    #endregion

    #region 技能数据

    public class SkillTableDatas
    {

        public static BindingList<SkillData> SkillTable = new();
        public static readonly object SkillTableLock = new();
    }
    public class SkillData : NotifyProperty
    {
        #region 字段（私有存储）
        private ulong skillId;    // 技能ID
        private string id;
        private string name;       // 技能名称
        private string icon;       // 技能图标（文件路径或URL）
        private CellText damage;      // 技能总伤害
        private CellText hitCount;      // 技能命中次数
        private CellText critRate;   // 暴击率
        private CellText avgPerHit;  // 平均值
        private CellProgress share; // 占比（0~1）
        private CellText totalDps;//秒伤
        private CellText percentage; //百分比
        private CellText highestnoncrit;
        private CellText highestcrit;
        private CellText interval; //time between last hit/activation


        #endregion

        #region 构造函数
        public SkillData(ulong skillId, string name, string icon, ulong damage, int hitCount, string critRate, double share, double avgPerHit, double totalDps, double highestnoncrit, double highestcrit, double interval)
        {
            SkillId = skillId;
            ID = skillId.ToString();
            Name = name;
            Icon = icon;
            Damage = new CellText(damage.ToString()) { Font = AppConfig.DigitalFont };
            HitCount = new CellText(hitCount.ToString()) { Font = AppConfig.DigitalFont };
            CritRate = new CellText(critRate) { Font = AppConfig.DigitalFont };

            Share = new CellProgress((float)share) { Fill = AppConfig.DpsColor, Size = new Size(200, 10) };
            AvgPerHit = new CellText(avgPerHit.ToString()) { Font = AppConfig.DigitalFont };
            TotalDps = new CellText(totalDps.ToString()) { Font = AppConfig.DigitalFont };
            Percentage = new CellText(share.ToString()) { Font = AppConfig.DigitalFont };

            HighestNonCrit = new CellText(highestnoncrit.ToString()) { Font = AppConfig.DigitalFont };
            HighestCrit = new CellText(highestcrit.ToString()) { Font = AppConfig.DigitalFont };

            Interval = new CellText(interval.ToString("F2")) { Font = AppConfig.DigitalFont };
        }
        #endregion

        #region 属性封装（包含通知）

        // —— 技能基础信息 —— 

        public CellText HighestNonCrit
        {
            get => highestnoncrit;
            set
            {
                if (highestnoncrit == value) return;
                highestnoncrit = value;
                OnPropertyChanged(nameof(HighestNonCrit));
            }
        }

        public CellText HighestCrit
        {
            get => highestcrit;
            set
            {
                if (highestcrit == value) return;
                highestcrit = value;
                OnPropertyChanged(nameof(HighestCrit));
            }
        }

        public CellText Interval
        {
            get => interval;
            set
            {
                if (interval == value) return;
                interval = value;
                OnPropertyChanged(nameof(Interval));
            }
        }

        public ulong SkillId
        {
            get => skillId;
            set
            {
                if (skillId == value) return;
                skillId = value;
                OnPropertyChanged(nameof(SkillId));
            }
        }

        public string ID
        {
            get => id;
            set
            {
                if (id == value) return;
                id = value;
                OnPropertyChanged(nameof(ID));
            }
        }


        /// <summary>
        /// 技能名称（用于UI显示）
        /// </summary>
        public string Name
        {
            get => name;
            set
            {
                if (name == value) return;
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        /// <summary>
        /// 技能图标（本地路径或URL）
        /// </summary>
        public string Icon
        {
            get => icon;
            set
            {
                if (icon == value) return;
                icon = value;
                OnPropertyChanged(nameof(Icon));
            }
        }

        // —— 技能统计数据 —— 

        /// <summary>
        /// 技能总伤害（累计值）
        /// </summary>
        public CellText Damage
        {
            get => damage;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value.Text));
                CellText formatted = new CellText(Common.FormatWithEnglishUnits(val)) { Font = AppConfig.DigitalFont };

                if (damage == formatted) return;
                damage = formatted;
                OnPropertyChanged(nameof(Damage));
            }
        }

        /// <summary>
        /// 技能命中次数
        /// </summary>
        public CellText HitCount
        {
            get => hitCount;
            set
            {
                if (hitCount == value) return;
                hitCount = value;
                OnPropertyChanged(nameof(HitCount));
            }
        }

        /// <summary>
        /// 暴击率（百分比字符串）
        /// </summary>
        public CellText CritRate
        {
            get => critRate;
            set
            {
                if (critRate == value) return;
                critRate = value;
                OnPropertyChanged(nameof(CritRate));
            }
        }



        /// <summary>
        /// 总伤害占比（0~1 之间的小数）
        /// </summary>
        public CellProgress Share
        {
            get => share;
            set
            {
                if (share == value) return;
                share = value;
                OnPropertyChanged(nameof(Share));
            }
        }

        public CellText AvgPerHit
        {
            get => avgPerHit;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value.Text));
                CellText formatted = new CellText(Common.FormatWithEnglishUnits(val)) { Font = AppConfig.DigitalFont };

                if (avgPerHit == formatted) return;
                avgPerHit = formatted;
                OnPropertyChanged(nameof(AvgPerHit));
            }
        }

        public CellText TotalDps
        {
            get => totalDps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value.Text));
                CellText formatted = new CellText(Common.FormatWithEnglishUnits(val)) { Font = AppConfig.DigitalFont };


                if (totalDps == formatted) return;
                totalDps = formatted;
                OnPropertyChanged(nameof(TotalDps));
            }
        }

        public CellText Percentage
        {
            get => percentage;
            set
            {
                string percentStr = Math.Round(double.Parse(value.Text) * 100).ToString();
                CellText formatted = new CellText(@$"{percentStr}%") { Font = AppConfig.DigitalFont };
                if (percentage == formatted) return;
                percentage = formatted;
                OnPropertyChanged(nameof(Percentage));
            }
        }
        #endregion
    }

    #endregion


    #region 排行榜
    public class LeaderboardTableDatas
    {
        /// <summary>
        /// 表格数据绑定
        /// </summary>
        public static BindingList<LeaderboardTable> LeaderboardTable = [];
        public static readonly object LeaderboardTableLock = new();

    }
    public class LeaderboardTable : NotifyProperty
    {
        #region 字段（私有存储）
        private string battleId;
        private string nickName;       // 玩家昵称
        private string professional; // 职业
        private CellText combatPower; // 战力
        private CellText totalDamage;     // 总伤害
        private CellText instantDps;     // 秒伤
        private CellText critRate;// 暴击率
        private CellText luckyRate; // 幸运率
        private CellText maxInstantDps;     // 最大瞬时DPS
        private CellText subProfessional; //分支职业
        private CellButton button;//按钮
        #endregion

        #region 构造函数
        public LeaderboardTable(string battleId, string nickName, string professional, double combatPower, double totalDamage, double instantDps, double critRate, double luckyRate, double maxInstantDps, string subProfessional)
        {
            BattleId = battleId;
            NickName = nickName;
            Professional = professional;
            CombatPower = new CellText(combatPower.ToString()) { Font = AppConfig.DigitalFont };
            TotalDamage = new CellText(totalDamage.ToString()) { Font = AppConfig.DigitalFont };
            InstantDps = new CellText(instantDps.ToString()) { Font = AppConfig.DigitalFont };
            MaxInstantDps = new CellText(maxInstantDps.ToString()) { Font = AppConfig.DigitalFont };
            SubProfessional = new CellText(subProfessional);
            CritRate = new CellText(critRate.ToString() + "%") { Font = AppConfig.DigitalFont };
            LuckyRate = new CellText(luckyRate.ToString() + "%") { Font = AppConfig.DigitalFont };
            Button = new CellButton("Button","查看技能数据",TTypeMini.Primary);
        }
        #endregion

        #region 属性封装（包含通知）

        public CellButton Button
        {
            get => button;
            set
            {
              
                button = value;
                //OnPropertyChanged(nameof(Button));
            }
        }

        // —— 玩家基础信息 —— 

        public string BattleId
        {
            get => battleId;
            set
            {
                if (battleId == value) return;
                battleId = value;
                OnPropertyChanged(nameof(BattleId));
            }
        }

        /// <summary>
        /// 玩家昵称
        /// </summary>
        public string NickName
        {
            get => nickName;
            set
            {
                if (nickName == value) return;
                nickName = value;
                OnPropertyChanged(nameof(NickName));
            }
        }

        /// <summary>
        /// 职业
        /// </summary>
        public string Professional
        {
            get => professional;
            set
            {
                if (professional == value) return;
                professional = value;
                OnPropertyChanged(nameof(Professional));
            }
        }

        public CellText SubProfessional
        {
            get => subProfessional;
            set
            {
                if (subProfessional == value) return;
                subProfessional = value;
                OnPropertyChanged(nameof(SubProfessional));
            }
        }

        /// <summary>
        /// 战力
        /// </summary>
        public CellText CombatPower
        {
            get => combatPower;
            set
            {
                if (combatPower == value) return;
                combatPower = value;
                OnPropertyChanged(nameof(CombatPower));
            }
        }

        // —— 玩家统计数据 —— 

        /// <summary>
        /// 总伤害
        /// </summary>
        public CellText TotalDamage
        {
            get => totalDamage;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value.Text));
                CellText formatted = new CellText(Common.FormatWithEnglishUnits(val)) { Font = AppConfig.DigitalFont };
                if (totalDamage == formatted) return;
                totalDamage = formatted;
                OnPropertyChanged(nameof(TotalDamage));
            }
        }

        /// <summary>
        /// 秒伤
        /// </summary>
        public CellText InstantDps
        {
            get => instantDps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value.Text));
                CellText formatted = new CellText(Common.FormatWithEnglishUnits(val)) { Font = AppConfig.DigitalFont };
                if (instantDps == formatted) return;
                instantDps = formatted;
                OnPropertyChanged(nameof(InstantDps));
            }
        }

        public CellText CritRate
        {
            get => critRate;
            set
            {
                if (critRate == value) return;
                critRate = value;
                OnPropertyChanged(nameof(CritRate));
            }
        }

        public CellText LuckyRate
        {
            get => luckyRate;
            set
            {
                if (luckyRate == value) return;
                luckyRate = value;
                OnPropertyChanged(nameof(LuckyRate));
            }
        }

        /// <summary>
        /// 最大瞬时DPS
        /// </summary>
        public CellText MaxInstantDps
        {
            get => maxInstantDps;
            set
            {
                ulong val = (ulong)Math.Floor(double.Parse(value.Text));
                CellText formatted = new CellText(Common.FormatWithEnglishUnits(val)) { Font = AppConfig.DigitalFont };
                if (maxInstantDps == formatted) return;
                maxInstantDps = formatted;
                OnPropertyChanged(nameof(MaxInstantDps));
            }
        }
        #endregion





    }


    #endregion

    #region 死亡统计
    public class DeathStatisticsTableDatas
    {
        /// <summary>
        /// 表格数据绑定
        /// </summary>
        public static BindingList<DeathStatisticsTable> DeathStatisticsTable = [];
        public static readonly object DeathStatisticsTableLock = new();
    }

    public class DeathStatisticsTable : NotifyProperty
    {
        #region 字段（私有存储）
        private ulong uid;
        private string nickName;       // 玩家昵称
        private int totalDeathCount;     // 死亡次数
        #endregion

        #region 构造函数
        public DeathStatisticsTable(ulong uid, string nickName,int totalDeathCount)
        {
            Uid = uid;
            NickName = nickName;
            TotalDeathCount =totalDeathCount;
        }
        public ulong Uid
        {
            get => uid;
            set
            {
                if (uid == value) return;
                uid =value;
                OnPropertyChanged(nameof(Uid));
            }
        }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName
        {
            get => nickName;
            set
            {
                if (nickName == value) return;
                nickName = value;
                OnPropertyChanged(nameof(NickName));
            }
        }
        /// <summary>
        /// 死亡次数
        /// </summary>
        public int TotalDeathCount
        {
            get => totalDeathCount;
            set
            {
                if (totalDeathCount == value) return;
                totalDeathCount = value;
                OnPropertyChanged(nameof(TotalDeathCount));
            }
        }


        #endregion

    }
    #endregion

    #region 伤害参考 技能数据
    public class DamageReferenceSkillData
    {
        public static BindingList<DamageReferenceSkill> DamageReferenceSkillTable = new();
        public static readonly object DamageReferenceSkillTableLock = new();
    }
    public class DamageReferenceSkill : NotifyProperty
    {
        #region 字段（私有存储）
        private string name;       // 技能名称
        private string damage;      // 技能总伤害
        private string hitCount;      // 技能命中次数
        private string critRate;   // 暴击率
        private string luckyRate;  // 幸运率
        private string avgPerHit;  // 平均值
        private string totalDps;//秒伤
        private string share; //百分比
        #endregion

        #region 构造函数
        public DamageReferenceSkill(string name,  string damage, int hitCount, string critRate, string luckyRate, string avgPerHit, string totalDps,double share)
        {
            Name = name;
            Damage = damage;
            HitCount = hitCount.ToString();
            CritRate = critRate;
            LuckyRate = luckyRate;
            AvgPerHit = avgPerHit.ToString();
            TotalDps = totalDps.ToString();
            Share = share.ToString("0.00") + "%";
         
        }

        public string Name
        {
            get => name;
            set
            {
                if (name == value) return;
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }


        public string Damage
        {
            get => damage;
            set
            {
                
                if (damage == value) return;
                damage = value;
                OnPropertyChanged(nameof(Damage));
            }
        }

        public string HitCount
        {
            get => hitCount;
            set
            {
                if (hitCount == value) return;
                hitCount = value;
                OnPropertyChanged(nameof(HitCount));
            }
        }

        public string CritRate
        {
            get => critRate;
            set
            {
                if (critRate == value) return;
                critRate = value;
                OnPropertyChanged(nameof(CritRate));
            }
        }

        public string LuckyRate
        {
            get => luckyRate;
            set
            {   
                if (luckyRate == value) return;
                luckyRate = value;
                OnPropertyChanged(nameof(LuckyRate));
            }
        }

        public string AvgPerHit
        {
            get => avgPerHit;
            set
            {
                if (avgPerHit == value) return;
                avgPerHit = value;
                OnPropertyChanged(nameof(AvgPerHit));
            }
        }

        public string TotalDps
        {
            get => totalDps;
            set
            {
                if (totalDps == value) return;
                totalDps = value;
                OnPropertyChanged(nameof(TotalDps));
            }
        }

        public string Share
        {
            get => share;
            set
            {
                if (share == value) return;
                share = value;
                OnPropertyChanged(nameof(Share));
            }
        }
        #endregion
    }


    #endregion
}

