using StarResonanceDpsAnalysis.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Forms
{
    public partial class HistoricalBattlesForm
    {
        public void ToggleTableView()
        {

            table_DpsDetailDataTable.Columns.Clear();

            table_DpsDetailDataTable.Columns = new AntdUI.ColumnCollection
            {
                /*
              new AntdUI.Column("Uid", "UID"),
                new AntdUI.Column("NickName", "昵称"), // im so lazy
                new AntdUI.Column("Profession", "职业"),
                new AntdUI.Column("CombatPower", "战力"),
                new AntdUI.Column("TotalDamage", "总伤害"),
                new AntdUI.Column("TotalDps", "秒伤"),
                new AntdUI.Column("CritRate", "暴击率"),
                new AntdUI.Column("LuckyRate", "幸运率"),
                new AntdUI.Column("CriticalDamage", "暴击伤害"),
                new AntdUI.Column("LuckyDamage", "幸运伤害"),
                new AntdUI.Column("CritLuckyDamage", "暴击且幸运的伤害"),
                new AntdUI.Column("MaxInstantDps", "最大瞬时Dps"),

                new AntdUI.Column("TotalHealingDone", "总治疗量"),
                new AntdUI.Column("TotalHps", "HPS/秒"),
                new AntdUI.Column("CriticalHealingDone", "暴击治疗量"),
                new AntdUI.Column("LuckyHealingDone", "幸运治疗量"),
                new AntdUI.Column("CritLuckyHealingDone", "暴击且幸运的治疗量"),
                new AntdUI.Column("MaxInstantHps", "最大瞬时 HPS"),
                new AntdUI.Column("DamageTaken", "承受伤害总量"),
               // new AntdUI.Column("Share","占比"),
                new AntdUI.Column("DmgShare","团队伤害占比(%)"),
                */
                              new AntdUI.Column("Uid", "UID"),
                new AntdUI.Column("NickName", "Name"), // im so lazy
                new AntdUI.Column("Profession", "Profession"),
                new AntdUI.Column("CombatPower", "Ability Score"),
                new AntdUI.Column("TotalDamage", "TotalDamage"),
                new AntdUI.Column("TotalDps", "TotalDps"),
                new AntdUI.Column("CritRate", "CritRate"),
                new AntdUI.Column("LuckyRate", "LuckyRate"),
                new AntdUI.Column("CriticalDamage", "CriticalDamage"),
                new AntdUI.Column("LuckyDamage", "LuckyDamage"),
                new AntdUI.Column("CritLuckyDamage", "CritLuckyDamage"),
                new AntdUI.Column("MaxInstantDps", "MaxInstantDps"),

                new AntdUI.Column("TotalHealingDone", "TotalHealing"),
                new AntdUI.Column("TotalHps", "TotalHps"),
                new AntdUI.Column("CriticalHealingDone", "CriticalHealingDone"),
                new AntdUI.Column("LuckyHealingDone", "LuckyHealingDone"),
                new AntdUI.Column("CritLuckyHealingDone", "CritLuckyHealingDone"),
                new AntdUI.Column("MaxInstantHps", "MaxInstantHps"),
                new AntdUI.Column("DamageTaken", "DamageTaken"),
               // new AntdUI.Column("Share","占比"),
                new AntdUI.Column("DmgShare","DmgShare(%)"),
            };

            table_DpsDetailDataTable.Binding(DpsTableDatas.DpsTable);


        }
    }


}
