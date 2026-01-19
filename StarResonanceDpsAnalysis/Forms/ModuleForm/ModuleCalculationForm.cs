using AntdUI;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Module;
using StarResonanceDpsAnalysis.Forms.PopUp;
using StarResonanceDpsAnalysis.Plugin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static StarResonanceDpsAnalysis.Core.Module.ModuleCardDisplay;

namespace StarResonanceDpsAnalysis.Forms.ModuleForm
{
    public partial class ModuleCalculationForm : BorderlessForm
    {

        public ModuleCalculationForm()
        {
            InitializeComponent();
            FormGui.SetDefaultGUI(this);

            ApplyLocalization();
        }

        private void ModuleCalculationForm_Load(object sender, EventArgs e)
        {
            FormGui.SetColorMode(this, AppConfig.IsLight);//设置窗体颜色
            if (Config.IsLight)
            {
                groupBox1.ForeColor = groupBox3.ForeColor = Color.Black;
            }
            else
            {
                groupBox1.ForeColor = groupBox3.ForeColor = Color.White;

            }
            TitleText.Font = AppConfig.SaoFont;
            label1.Font = groupBox1.Font = AppConfig.ContentFont;
            button1.Font = AppConfig.ContentFont;

            List<Select> selects = new List<Select>
{
    select1,
    select2,
    select4,
    select5,
    select6,
    select7,
    select8,
    select9,
    select10,
    select11,
    select12,
    select3
};
            List<InputNumber> inputNumbers = new List<InputNumber>
{
    inputNumber1,
    inputNumber2,
    inputNumber3,
    inputNumber4,
    inputNumber5
};
            foreach (var sel in selects)
            {
                sel.Font = AppConfig.ContentFont;
            }

            foreach (var num in inputNumbers)
            {
                num.Font = AppConfig.ContentFont;
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
            if (MessageAnalyzer.PayloadBuffer.Length == 0)
            {
                var result = AppMessageBox.ShowMessage(Properties.Strings.Notice_ChangeMaps, this);
                return;
            }


            BuildEliteCandidatePool.ParseModuleInfo(MessageAnalyzer.PayloadBuffer);



            virtualPanel1.Waterfall = false;      // 行优先（有 Align 的话设 Start）
            virtualPanel1.Items.Clear();

            // 取数据并先实例化所有卡（还不加到 panel）
            var cards = new List<ModuleCardDisplay.ResultCardSimpleItem>();
            foreach (var dto in ModuleCardDisplay.ModuleResultMemory.GetSnapshot())
            {
                cards.Add(new ModuleCardDisplay.ResultCardSimpleItem
                {
                    RankText = dto.RankText,
                    HighestLevel = dto.HighestLevel,
                    Score = dto.Score,        // 你现在是 string
                    ModuleRows = dto.ModuleRows,
                    AttrLines = dto.AttrLines
                });
            }

            // ========== 第一步：统一列宽 ==========
            float dpi = AntdUI.Config.Dpi;
            int gutterPx = (int)MathF.Round(12 * dpi);     // 列间距
            int minCardW = (int)MathF.Round(240 * dpi);    // 最小卡宽
            int maxCols = 3;                              // 最多列
            int panelW = Math.Max(1, virtualPanel1.ClientSize.Width);

            // 用与卡片一致的字体
            var baseFont = virtualPanel1.Font ?? SystemFonts.DefaultFont;
            using var fontBody = new Font(baseFont, baseFont.Style);
            using var fontTitle = new Font(baseFont, FontStyle.Bold);

            // 计算每张卡“期望宽度”：左右 Padding + 模组列表里“最长一行（左+gap+右）”
            int ComputePreferredCardWidth(ModuleCardDisplay.ResultCardSimpleItem card)
            {
                int padL = (int)MathF.Round(card.ContentPadding.Left * dpi);
                int padR = (int)MathF.Round(card.ContentPadding.Right * dpi);
                int gap = (int)MathF.Round(card.LeftRightGap * dpi);

                int maxRowContentW = 0;
                foreach (var row in card.ModuleRows ?? Enumerable.Empty<(string Left, string Right)>())
                {
                    var left = row.Left ?? string.Empty;
                    var right = $"[{row.Right ?? string.Empty}]";
                    int leftW = TextRenderer.MeasureText(left, fontBody).Width;
                    int rightW = TextRenderer.MeasureText(right, fontBody).Width;
                    maxRowContentW = Math.Max(maxRowContentW, leftW + gap + rightW);
                }
                int preferred = padL + maxRowContentW + padR;
                preferred = Math.Max(preferred, minCardW);
                preferred = Math.Min(preferred, panelW);
                return preferred;
            }

            int preferredMaxW = cards.Count == 0 ? minCardW : cards.Max(ComputePreferredCardWidth);

            // 算能排几列，并反推统一卡宽
            int cols = Math.Max(1, Math.Min(maxCols, (panelW + gutterPx) / (preferredMaxW + gutterPx)));
            int unifiedCardW = (panelW - gutterPx * (cols - 1)) / cols;
            unifiedCardW = Math.Max(1, Math.Min(unifiedCardW, panelW));

            // 把统一宽写到每张卡
            foreach (var c in cards) c.ForceCardWidthPx = unifiedCardW;

            // ========== 第二步：统一高度 ==========
            // 用统一宽度测每张卡高度，取最大值
            int MeasureCardHeight(ModuleCardDisplay.ResultCardSimpleItem card)
            {
                int padL = (int)MathF.Round(card.ContentPadding.Left * dpi);
                int padT = (int)MathF.Round(card.ContentPadding.Top * dpi);
                int padR = (int)MathF.Round(card.ContentPadding.Right * dpi);
                int padB = (int)MathF.Round(card.ContentPadding.Bottom * dpi);
                int gap = (int)MathF.Round(card.LeftRightGap * dpi);

                int contentW = Math.Max(1, unifiedCardW - padL - padR);
                int y = 0;

                // 标题
                y += TextRenderer.MeasureText(card.RankText ?? "", fontTitle,
                      new Size(contentW, int.MaxValue), TextFormatFlags.WordBreak).Height + card.LineGap;

                // 最高属性等级
                y += TextRenderer.MeasureText($"{Properties.Strings.ModuleCalc_TotalScore}{card.HighestLevel}", fontBody,
                      new Size(contentW, int.MaxValue), TextFormatFlags.WordBreak).Height + card.LineGap;

                // 综合评分（两段同一行，测一整段足够）
                string scoreText = string.IsNullOrEmpty(card.Score) ? "—" : card.Score;

                y += TextRenderer.MeasureText($"{Properties.Strings.ModuleCalc_TotalScore}{scoreText}", fontBody,
                      new Size(contentW, int.MaxValue), TextFormatFlags.WordBreak).Height + card.SectionGap;

                // 列表标题
                y += TextRenderer.MeasureText(Properties.Strings.ModuleCalc_ModuleList, fontBody,
                      new Size(contentW, int.MaxValue), TextFormatFlags.WordBreak).Height + card.LineGap;

                // 列表每行（左列宽 = contentW - 右列自然宽 - gap；右列自然宽不换行）
                foreach (var row in card.ModuleRows ?? Enumerable.Empty<(string Left, string Right)>())
                {
                    var left = row.Left ?? string.Empty;
                    var right = $"[{row.Right ?? string.Empty}]";
                    int rightW = TextRenderer.MeasureText(right, fontBody).Width;
                    int leftMaxW = Math.Max(1, contentW - rightW - gap);

                    int hL = TextRenderer.MeasureText(left, fontBody,
                             new Size(leftMaxW, int.MaxValue), TextFormatFlags.WordBreak).Height;
                    int hR = TextRenderer.MeasureText(right, fontBody).Height;

                    y += Math.Max(hL, hR) + 4;
                }

                y += card.SectionGap;

                // 属性分布标题
                y += TextRenderer.MeasureText(Properties.Strings.ModuleCalc_AttrEffects, fontBody,
                      new Size(contentW, int.MaxValue), TextFormatFlags.WordBreak).Height + card.LineGap;

                // 属性分布每行
                foreach (var (name, val) in card.AttrLines ?? Enumerable.Empty<(string Name, int Value)>())
                {
                    string line = $"{name}：+{val}";
                    y += TextRenderer.MeasureText(line, fontBody,
                         new Size(contentW, int.MaxValue), TextFormatFlags.WordBreak).Height + 4;
                }

                int minH = (int)(card.MinHeightDp * dpi);
                return Math.Max(minH, y + padT + padB);
            }

            int unifiedCardH = cards.Count == 0 ? (int)(160 * dpi) : cards.Max(MeasureCardHeight);
            foreach (var c in cards) c.ForceCardHeightPx = unifiedCardH;

            // ========== 第三步：一次性加入并刷新 ==========
            virtualPanel1.SuspendLayout();
            foreach (var c in cards) virtualPanel1.Items.Add(c);
            virtualPanel1.ResumeLayout();
            virtualPanel1.Refresh();              // 一次刷新即可看到统一宽高效果


        }

        private void chkAttackSpeedFocus_CheckedChanged(object sender, BoolEventArgs e)
        {
            Checkbox checkbox = (Checkbox)sender;
            if (e.Value)
            {

                BuildEliteCandidatePool.Attributes.Add(checkbox.Text);
            }
            else
            {

                BuildEliteCandidatePool.Attributes.Remove(checkbox.Text);
            }
        }

        private void select1_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            BuildEliteCandidatePool.type = select1.SelectedValue.ToString();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            MessageBox.Show("装饰用 (For Decoration)");
        }

        private void ModuleCalculationForm_ForeColorChanged(object sender, EventArgs e)
        {
            if (Config.IsLight)
            {
                groupBox1.ForeColor = groupBox3.ForeColor = Color.Black;
            }
            else
            {
                groupBox1.ForeColor = groupBox3.ForeColor = Color.White;

            }

        }
        ModuleExcludeForm moduleExcludeForm = null;
        private void button2_Click(object sender, EventArgs e)
        {

            if (moduleExcludeForm == null || moduleExcludeForm.IsDisposed)
            {
                moduleExcludeForm = new ModuleExcludeForm();
            }
            moduleExcludeForm.Show();
        }



        private void select2_SelectedIndexChanged(object sender, IntEventArgs e)
        {
            // 通用：5 个下拉都可指向这个事件，靠 Tag 区分第几行
            if (sender is Select combo)
            {
                int rowIndex = 0;
                if (combo.Tag != null) int.TryParse(combo.Tag.ToString(), out rowIndex);

                string selectedAttr = combo.SelectedValue?.ToString() ?? string.Empty;

                // 旧的该行词条（若存在，等会要从 Attributes/DesiredLevels 里移干净）
                string old = null;
                if (BuildEliteCandidatePool.WhitelistPickByRow.TryGetValue(rowIndex, out var oldName))
                    old = oldName;

                // 写入“按行”映射
                if (!string.IsNullOrEmpty(selectedAttr))
                    BuildEliteCandidatePool.WhitelistPickByRow[rowIndex] = selectedAttr;
                else
                    BuildEliteCandidatePool.WhitelistPickByRow.Remove(rowIndex);

                // 维护 Attributes（白名单集合）：= 所有行里挑出的非空属性去重集合
                var newWhitelist = BuildEliteCandidatePool.WhitelistPickByRow.Values
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                BuildEliteCandidatePool.Attributes.Clear();
                BuildEliteCandidatePool.Attributes.AddRange(newWhitelist);  // 供 UI 最高等级展示与优先判定

                // 如果换了词条，把旧词条的期望等级也删掉，避免残留
                if (!string.IsNullOrEmpty(old) && old != selectedAttr)
                {
                    BuildEliteCandidatePool.DesiredLevels.Remove(old);
                }
                // —— 互斥：若所选属性在“排除集合”里，自动撤销排除并清掉排除UI
                if (!string.IsNullOrEmpty(selectedAttr)
                    && BuildEliteCandidatePool.ExcludedAttributes.Contains(selectedAttr))
                {
                    // 1) 集合中移除
                    BuildEliteCandidatePool.ExcludedAttributes.Remove(selectedAttr);

                    // 2) 清空排除区所有等于 selectedAttr 的下拉
                    foreach (var exSel in GetAllExcludeSelects())
                    {
                        if (exSel?.SelectedValue?.ToString() == selectedAttr)
                        {
                            exSel.SelectedIndex = -1;
                            exSel.Tag = null; // 你之前在排除事件里用 Tag 记旧值，这里同步清掉
                        }
                    }
                }
            }
        }

        private void inputNumber1_ValueChanged(object sender, DecimalEventArgs e)
        {
            if (sender is InputNumber num)
            {
                int rowIndex = 0;
                if (num.Tag != null) int.TryParse(num.Tag.ToString(), out rowIndex);

                // 找这一行当前绑定的白名单属性
                if (!BuildEliteCandidatePool.WhitelistPickByRow.TryGetValue(rowIndex, out var attrName)
                    || string.IsNullOrEmpty(attrName))
                {
                    return; // 行未选属性，忽略
                }

                // 直接读取 Value（decimal 类型）
                int desiredLevel = Convert.ToInt32(num.Value);
                desiredLevel = Math.Max(0, Math.Min(6, desiredLevel));

                if (desiredLevel > 0)
                    BuildEliteCandidatePool.DesiredLevels[attrName] = desiredLevel;
                else
                    BuildEliteCandidatePool.DesiredLevels.Remove(attrName);
            }
        }

        private void select12_SelectedIndexChanged_1(object sender, IntEventArgs e)
        {
            if (sender is AntdUI.Select combo)
            {
                // 先清除旧的（避免重复/冲突）
                BuildEliteCandidatePool.ExcludedAttributes.RemoveWhere(x => x == (string)combo.Tag);

                // 新选项
                string selectedAttr = combo.SelectedValue?.ToString();

                if (!string.IsNullOrEmpty(selectedAttr))
                {
                    // 用 Tag 标识是第几个下拉，避免重复覆盖
                    combo.Tag = selectedAttr;
                    BuildEliteCandidatePool.ExcludedAttributes.Add(selectedAttr);
                }
                // —— 互斥：若排除了 selectedAttr，就把目标白名单里所有等于它的行清掉（不动期望等级）
                if (!string.IsNullOrEmpty(selectedAttr))
                {
                    // 找到所有匹配的行（可能多行选了同一属性）
                    var hitRows = BuildEliteCandidatePool.WhitelistPickByRow
                        .Where(kv => string.Equals(kv.Value, selectedAttr, StringComparison.Ordinal))
                        .Select(kv => kv.Key)
                        .ToList();

                    if (hitRows.Count > 0)
                    {
                        // 1) 从“行→属性”的映射中移除这些行（不删 DesiredLevels，保留用户设定）
                        foreach (var r in hitRows)
                        {
                            BuildEliteCandidatePool.WhitelistPickByRow.Remove(r);

                            // 2) 清空这些行的目标下拉 UI
                            var wlSel = GetWhitelistSelectByRow(r);
                            if (wlSel != null) wlSel.SelectedIndex = -1;
                            // 注意：不去改对应的 InputNumber，等级保留
                        }

                        // 3) 重建 Attributes（白名单集合）
                        var newWhitelist = BuildEliteCandidatePool.WhitelistPickByRow.Values
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Distinct(StringComparer.Ordinal)
                            .ToList();
                        BuildEliteCandidatePool.Attributes.Clear();
                        BuildEliteCandidatePool.Attributes.AddRange(newWhitelist);
                    }
                }

            }
        }

        private void select3_SelectedIndexChanged(object sender, IntEventArgs e)
        {

        }

        private void select2_ClearClick(object sender, MouseEventArgs e)
        {

        }

        /// <summary>
        /// 右键清除排除属性
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void selectEmptyRule_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            if (sender is AntdUI.Select combo)
            {
                // 移除掉之前存进去的排除属性
                if (combo.Tag is string old && !string.IsNullOrEmpty(old))
                {
                    BuildEliteCandidatePool.ExcludedAttributes.Remove(old);
                }

                // 清空下拉显示（如果 AntdUI.Select 支持）
                combo.SelectedIndex = -1;

                // 清空 Tag
                combo.Tag = null;
            }
        }

        /// <summary>
        /// 清除所有行的属性选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void selectClearSelection_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (sender is AntdUI.Select combo)
            {
                if (!(combo.Tag is string tagStr) || !int.TryParse(tagStr, out int row)) return;

                // 1) 找到这行当前绑定的白名单属性
                if (BuildEliteCandidatePool.WhitelistPickByRow.TryGetValue(row, out var attrName)
                    && !string.IsNullOrEmpty(attrName))
                {
                    // 2) 删期望等级
                    BuildEliteCandidatePool.DesiredLevels.Remove(attrName);

                    // 3) 删“按行绑定”
                    BuildEliteCandidatePool.WhitelistPickByRow.Remove(row);
                }

                // 4) 重建 Attributes（= 所有行的非空属性去重集合）
                var newWhitelist = BuildEliteCandidatePool.WhitelistPickByRow.Values
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                BuildEliteCandidatePool.Attributes.Clear();
                BuildEliteCandidatePool.Attributes.AddRange(newWhitelist);

                // 5) 清空 UI：本行下拉和该行的期望等级数值框
                combo.SelectedIndex = -1;
                GetDesiredLevelControlByRow(row).Value = 0;

                // （不改 combo.Tag，Tag 保持“行号”，方便继续用）
            }
        }
        private AntdUI.InputNumber GetDesiredLevelControlByRow(int row)
        {
            return row switch
            {
                0 => inputNumber1,
                1 => inputNumber2,
                2 => inputNumber3,
                3 => inputNumber4,
                4 => inputNumber5,
                _ => null
            };
        }

        private AntdUI.Select GetWhitelistSelectByRow(int row)
        {
            return row switch
            {
                0 => select2,
                1 => select4,
                2 => select5,
                3 => select6,
                4 => select7,
                _ => null
            };
        }

        private IEnumerable<AntdUI.Select> GetAllExcludeSelects()
        {
            // 你的排除下拉（按你实际命名补全）
            yield return select8;
            yield return select9;
            yield return select10;
            yield return select11;
            yield return select12;
        }

        private void select3_SelectedIndexChanged_1(object sender, IntEventArgs e)
        {
            // e.Value 就是选中的索引
            BuildEliteCandidatePool.SortBy = (e.Value == 0)
                ? ModuleOptimizer.SortMode.ByTotalAttr
                : ModuleOptimizer.SortMode.ByScore;
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 1) 清空这次界面的所有“目标/排除/等级”选择
            BuildEliteCandidatePool.ResetSelections(keepSortMode: false);
            // keepSortMode=false：连排序模式也恢复到默认（上面方法里指定的默认）。
            // 若你想保留用户上次的排序模式：传 true。

            // 2)（可选）清空界面卡片缓存/显示
            try
            {
                virtualPanel1?.Items?.Clear();
                virtualPanel1?.Refresh();
            }
            catch { /* 忽略释放时控件已销毁的异常 */ }

            base.OnFormClosed(e);
        }

        public void ApplyLocalization()
        {
            // Title and labels
            TitleText.Text = Properties.Strings.ModuleCalc_Title;
            label1.Text = Properties.Strings.ModuleCalc_Label1;

            // Group boxes
            groupBox1.Text = Properties.Strings.ModuleCalc_GroupBox1;
            groupBox3.Text = Properties.Strings.ModuleCalc_GroupBox3;

            // Buttons
            button1.Text = Properties.Strings.ModuleCalc_Button1;
            // button2.Text = Properties.Strings.ModuleCalc_Button2;

            //select1.PrefixText = Properties.Strings.ModuleCalc_Select1_Prefix;
            //select2.PrefixText = Properties.Strings.ModuleCalc_Select2_Prefix;
            //select3.PrefixText = Properties.Strings.ModuleCalc_Select3_Prefix;
            //select4.PrefixText = Properties.Strings.ModuleCalc_Select4_Prefix;
            //select5.PrefixText = Properties.Strings.ModuleCalc_Select5_Prefix;
            //select6.PrefixText = Properties.Strings.ModuleCalc_Select6_Prefix;
            //select7.PrefixText = Properties.Strings.ModuleCalc_Select7_Prefix;
            //select8.PrefixText = Properties.Strings.ModuleCalc_Select8_Prefix;
            //select9.PrefixText = Properties.Strings.ModuleCalc_Select9_Prefix;
            //select10.PrefixText = Properties.Strings.ModuleCalc_Select10_Prefix;
            //select11.PrefixText = Properties.Strings.ModuleCalc_Select11_Prefix;
            //select12.PrefixText = Properties.Strings.ModuleCalc_Select12_Prefix;

            select1.PrefixText = "";
            select2.PrefixText = "";
            select3.PrefixText = "";
            select4.PrefixText = "";
            select5.PrefixText = "";
            select6.PrefixText = "";
            select7.PrefixText = "";
            select8.PrefixText = "";
            select9.PrefixText = "";
            select10.PrefixText = "";
            select11.PrefixText = "";
            select12.PrefixText = "";

            // Selects (dropdowns) - Text and Items
            select1.SelectedValue = Properties.Strings.ModuleCategory_All;
            select1.Text = Properties.Strings.ModuleCategory_All;
            select1.Items.Clear();
            select1.Items.AddRange(new[]
            {
                Properties.Strings.ModuleCategory_Attack,
                Properties.Strings.ModuleCategory_Support,
                Properties.Strings.ModuleCategory_Guardian,
                Properties.Strings.ModuleCategory_All
            });

            select2.Text = Properties.Strings.ModuleCalc_Select2;
            select2.Items.Clear();
            select2.Items.AddRange(new[]
            {
                Properties.Strings.ModuleAttr_StrengthBoost,
                Properties.Strings.ModuleAttr_AgilityBoost,
                Properties.Strings.ModuleAttr_IntelligenceBoost,
                Properties.Strings.ModuleAttr_SpecialAttackDamage,
                Properties.Strings.ModuleAttr_EliteStrike,
                Properties.Strings.ModuleAttr_SpecialHealingBoost,
                Properties.Strings.ModuleAttr_ExpertHealingBoost,
                Properties.Strings.ModuleAttr_CastingFocus,
                Properties.Strings.ModuleAttr_AttackSpeedFocus,
                Properties.Strings.ModuleAttr_CriticalFocus,
                Properties.Strings.ModuleAttr_LuckFocus,
                Properties.Strings.ModuleAttr_MagicResistance,
                Properties.Strings.ModuleAttr_PhysicalResistance,
                Properties.Strings.ModuleAttr_ExtremeDamageStack,
                Properties.Strings.ModuleAttr_ExtremeFlexibleMovement,
                Properties.Strings.ModuleAttr_ExtremeLifeConvergence,
                Properties.Strings.ModuleAttr_ExtremeEmergencyMeasures,
                Properties.Strings.ModuleAttr_ExtremeLifeFluctuation,
                Properties.Strings.ModuleAttr_ExtremeLifeDrain,
                Properties.Strings.ModuleAttr_ExtremeTeamCrit,
                Properties.Strings.ModuleAttr_ExtremeDesperateGuardian
            });

            select3.Text = Properties.Strings.ModuleCalc_Select3_Item_SortByScore;
            select3.Items.Clear();
            select3.Items.AddRange(new[]
            {
                Properties.Strings.ModuleCalc_Select3_Item_SortByTotalAttr,
                Properties.Strings.ModuleCalc_Select3_Item_SortByScore
            });
            select3.SelectedValue = Properties.Strings.ModuleCalc_Select3_Item_SortByScore;

            select4.Text = Properties.Strings.ModuleCalc_Select4;
            select4.Items.Clear();
            select4.Items.AddRange(new[]
            {
                Properties.Strings.ModuleAttr_StrengthBoost,
                Properties.Strings.ModuleAttr_AgilityBoost,
                Properties.Strings.ModuleAttr_IntelligenceBoost,
                Properties.Strings.ModuleAttr_SpecialAttackDamage,
                Properties.Strings.ModuleAttr_EliteStrike,
                Properties.Strings.ModuleAttr_SpecialHealingBoost,
                Properties.Strings.ModuleAttr_ExpertHealingBoost,
                Properties.Strings.ModuleAttr_CastingFocus,
                Properties.Strings.ModuleAttr_AttackSpeedFocus,
                Properties.Strings.ModuleAttr_CriticalFocus,
                Properties.Strings.ModuleAttr_LuckFocus,
                Properties.Strings.ModuleAttr_MagicResistance,
                Properties.Strings.ModuleAttr_PhysicalResistance,
                Properties.Strings.ModuleAttr_ExtremeDamageStack,
                Properties.Strings.ModuleAttr_ExtremeFlexibleMovement,
                Properties.Strings.ModuleAttr_ExtremeLifeConvergence,
                Properties.Strings.ModuleAttr_ExtremeEmergencyMeasures,
                Properties.Strings.ModuleAttr_ExtremeLifeFluctuation,
                Properties.Strings.ModuleAttr_ExtremeLifeDrain,
                Properties.Strings.ModuleAttr_ExtremeTeamCrit,
                Properties.Strings.ModuleAttr_ExtremeDesperateGuardian
            });

            select5.Text = Properties.Strings.ModuleCalc_Select5;
            select5.Items.Clear();
            select5.Items.AddRange(new[]
            {
                Properties.Strings.ModuleAttr_StrengthBoost,
                Properties.Strings.ModuleAttr_AgilityBoost,
                Properties.Strings.ModuleAttr_IntelligenceBoost,
                Properties.Strings.ModuleAttr_SpecialAttackDamage,
                Properties.Strings.ModuleAttr_EliteStrike,
                Properties.Strings.ModuleAttr_SpecialHealingBoost,
                Properties.Strings.ModuleAttr_ExpertHealingBoost,
                Properties.Strings.ModuleAttr_CastingFocus,
                Properties.Strings.ModuleAttr_AttackSpeedFocus,
                Properties.Strings.ModuleAttr_CriticalFocus,
                Properties.Strings.ModuleAttr_LuckFocus,
                Properties.Strings.ModuleAttr_MagicResistance,
                Properties.Strings.ModuleAttr_PhysicalResistance,
                Properties.Strings.ModuleAttr_ExtremeDamageStack,
                Properties.Strings.ModuleAttr_ExtremeFlexibleMovement,
                Properties.Strings.ModuleAttr_ExtremeLifeConvergence,
                Properties.Strings.ModuleAttr_ExtremeEmergencyMeasures,
                Properties.Strings.ModuleAttr_ExtremeLifeFluctuation,
                Properties.Strings.ModuleAttr_ExtremeLifeDrain,
                Properties.Strings.ModuleAttr_ExtremeTeamCrit,
                Properties.Strings.ModuleAttr_ExtremeDesperateGuardian
            });

            select6.Text = Properties.Strings.ModuleCalc_Select6; //todo come back here
            select6.Items.Clear();
            select6.Items.AddRange(new[]
            {
                Properties.Strings.ModuleAttr_StrengthBoost,
                Properties.Strings.ModuleAttr_AgilityBoost,
                Properties.Strings.ModuleAttr_IntelligenceBoost,
                Properties.Strings.ModuleAttr_SpecialAttackDamage,
                Properties.Strings.ModuleAttr_EliteStrike,
                Properties.Strings.ModuleAttr_SpecialHealingBoost,
                Properties.Strings.ModuleAttr_ExpertHealingBoost,
                Properties.Strings.ModuleAttr_CastingFocus,
                Properties.Strings.ModuleAttr_AttackSpeedFocus,
                Properties.Strings.ModuleAttr_CriticalFocus,
                Properties.Strings.ModuleAttr_LuckFocus,
                Properties.Strings.ModuleAttr_MagicResistance,
                Properties.Strings.ModuleAttr_PhysicalResistance,
                Properties.Strings.ModuleAttr_ExtremeDamageStack,
                Properties.Strings.ModuleAttr_ExtremeFlexibleMovement,
                Properties.Strings.ModuleAttr_ExtremeLifeConvergence,
                Properties.Strings.ModuleAttr_ExtremeEmergencyMeasures,
                Properties.Strings.ModuleAttr_ExtremeLifeFluctuation,
                Properties.Strings.ModuleAttr_ExtremeLifeDrain,
                Properties.Strings.ModuleAttr_ExtremeTeamCrit,
                Properties.Strings.ModuleAttr_ExtremeDesperateGuardian
            });

            select7.Text = Properties.Strings.ModuleCalc_Select7;
            select7.Items.Clear();
            select7.Items.AddRange(new[]
            {
                Properties.Strings.ModuleAttr_StrengthBoost,
                Properties.Strings.ModuleAttr_AgilityBoost,
                Properties.Strings.ModuleAttr_IntelligenceBoost,
                Properties.Strings.ModuleAttr_SpecialAttackDamage,
                Properties.Strings.ModuleAttr_EliteStrike,
                Properties.Strings.ModuleAttr_SpecialHealingBoost,
                Properties.Strings.ModuleAttr_ExpertHealingBoost,
                Properties.Strings.ModuleAttr_CastingFocus,
                Properties.Strings.ModuleAttr_AttackSpeedFocus,
                Properties.Strings.ModuleAttr_CriticalFocus,
                Properties.Strings.ModuleAttr_LuckFocus,
                Properties.Strings.ModuleAttr_MagicResistance,
                Properties.Strings.ModuleAttr_PhysicalResistance,
                Properties.Strings.ModuleAttr_ExtremeDamageStack,
                Properties.Strings.ModuleAttr_ExtremeFlexibleMovement,
                Properties.Strings.ModuleAttr_ExtremeLifeConvergence,
                Properties.Strings.ModuleAttr_ExtremeEmergencyMeasures,
                Properties.Strings.ModuleAttr_ExtremeLifeFluctuation,
                Properties.Strings.ModuleAttr_ExtremeLifeDrain,
                Properties.Strings.ModuleAttr_ExtremeTeamCrit,
                Properties.Strings.ModuleAttr_ExtremeDesperateGuardian
            });

            // Exclude selects (if you want to localize their text)
            select8.Text = Properties.Strings.ModuleCalc_Select8;
            select9.Text = Properties.Strings.ModuleCalc_Select9;
            select10.Text = Properties.Strings.ModuleCalc_Select10;
            select11.Text = Properties.Strings.ModuleCalc_Select11;
            select12.Text = Properties.Strings.ModuleCalc_Select12;

            select8.Items.Clear();
            select8.Items.AddRange(new[]
            {
                Properties.Strings.ModuleAttr_StrengthBoost,
                Properties.Strings.ModuleAttr_AgilityBoost,
                Properties.Strings.ModuleAttr_IntelligenceBoost,
                Properties.Strings.ModuleAttr_SpecialAttackDamage,
                Properties.Strings.ModuleAttr_EliteStrike,
                Properties.Strings.ModuleAttr_SpecialHealingBoost,
                Properties.Strings.ModuleAttr_ExpertHealingBoost,
                Properties.Strings.ModuleAttr_CastingFocus,
                Properties.Strings.ModuleAttr_AttackSpeedFocus,
                Properties.Strings.ModuleAttr_CriticalFocus,
                Properties.Strings.ModuleAttr_LuckFocus,
                Properties.Strings.ModuleAttr_MagicResistance,
                Properties.Strings.ModuleAttr_PhysicalResistance,
                Properties.Strings.ModuleAttr_ExtremeDamageStack,
                Properties.Strings.ModuleAttr_ExtremeFlexibleMovement,
                Properties.Strings.ModuleAttr_ExtremeLifeConvergence,
                Properties.Strings.ModuleAttr_ExtremeEmergencyMeasures,
                Properties.Strings.ModuleAttr_ExtremeLifeFluctuation,
                Properties.Strings.ModuleAttr_ExtremeLifeDrain,
                Properties.Strings.ModuleAttr_ExtremeTeamCrit,
                Properties.Strings.ModuleAttr_ExtremeDesperateGuardian
            });

            select9.Items.Clear();
            select9.Items.AddRange(select8.Items.Cast<string>().ToArray());

            select10.Items.Clear();
            select10.Items.AddRange(select8.Items.Cast<string>().ToArray());

            select11.Items.Clear();
            select11.Items.AddRange(select8.Items.Cast<string>().ToArray());

            select12.Items.Clear();
            select12.Items.AddRange(select8.Items.Cast<string>().ToArray());

            inputNumber1.PrefixText = Properties.Strings.ModuleCalc_InputNumber1_Prefix;
            inputNumber2.PrefixText = Properties.Strings.ModuleCalc_InputNumber2_Prefix;
            inputNumber3.PrefixText = Properties.Strings.ModuleCalc_InputNumber3_Prefix;
            inputNumber4.PrefixText = Properties.Strings.ModuleCalc_InputNumber4_Prefix;
            inputNumber5.PrefixText = Properties.Strings.ModuleCalc_InputNumber5_Prefix;
        }
    }
}
